using System.Collections.Concurrent;
using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Singleton service that processes batches of offline transactions from mobile clients.
/// Supports dual-timestamp sync records, damage flags, condition-note returns, and
/// last-write-wins conflict handling with coordinator override.
/// </summary>
public class OfflineSyncService : IOfflineSyncService
{
    private readonly IEquipmentService _equipmentService;
    private readonly ICoordinatorNotificationService _notificationService;
    private readonly IUserService _userService;

    private readonly ConcurrentDictionary<string, SyncResult> _results = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, OfflineSyncTransaction> _transactions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ConflictOverrideAuditEntry> _overrideAuditLog = new();
    private readonly object _batchLock = new();

    public OfflineSyncService(
        IEquipmentService equipmentService,
        ICoordinatorNotificationService notificationService,
        IUserService userService)
    {
        _equipmentService = equipmentService;
        _notificationService = notificationService;
        _userService = userService;
    }

    public IReadOnlyList<SyncResult> ProcessBatch(
        IReadOnlyList<OfflineSyncTransaction> transactions,
        int requestingUserId)
    {
        if (transactions is null || transactions.Count == 0)
            return Array.Empty<SyncResult>();

        var checkoutWinnerIds = transactions
            .Where(t => string.Equals(t.Type, "checkout", StringComparison.OrdinalIgnoreCase))
            .GroupBy(t => t.ItemId)
            .Where(g => g.Count() > 1)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(t => t.OfflineTimestamp).First().DeviceTransactionId);

        var ordered = transactions.OrderBy(t => t.OfflineTimestamp).ToList();
        var batchResults = new List<SyncResult>(ordered.Count);

        lock (_batchLock)
        {
            foreach (var tx in ordered)
            {
                _transactions[tx.DeviceTransactionId] = tx;

                if (string.Equals(tx.Type, "checkout", StringComparison.OrdinalIgnoreCase) &&
                    checkoutWinnerIds.TryGetValue(tx.ItemId, out var winnerId) &&
                    !string.Equals(winnerId, tx.DeviceTransactionId, StringComparison.OrdinalIgnoreCase))
                {
                    var lostItem = _equipmentService.GetItem(tx.ItemId);
                    string conflictMsg =
                        $"Within-batch LWW conflict for item {tx.ItemId}: transaction {tx.DeviceTransactionId} " +
                        $"(ts: {tx.OfflineTimestamp:O}) was superseded by a later checkout in the same batch.";
                    string workerMsg =
                        $"Your checkout of '{lostItem?.Name ?? tx.ItemId.ToString()}' was not applied — " +
                        "a more recent record was received in the same sync. Your coordinator has been notified.";

                    var lostResult = new SyncResult
                    {
                        DeviceTransactionId = tx.DeviceTransactionId,
                        Status = "conflict",
                        ConflictDetails = conflictMsg,
                        PlainLanguageMessage = workerMsg,
                        WorkerMessage = workerMsg
                    };

                    _results[tx.DeviceTransactionId] = lostResult;
                    batchResults.Add(lostResult);
                    NotifyCoordinators(0, conflictMsg);
                    continue;
                }

                var result = ProcessSingle(tx, requestingUserId);
                _results[tx.DeviceTransactionId] = result;
                batchResults.Add(result);
            }
        }

        return batchResults.AsReadOnly();
    }

    public SyncResult? GetResult(string deviceTransactionId)
    {
        _results.TryGetValue(deviceTransactionId, out var result);
        return result;
    }

    public SyncResult? CoordinatorOverride(
        string deviceTransactionId,
        int coordinatorUserId,
        string overrideReason)
    {
        if (!_transactions.TryGetValue(deviceTransactionId, out var tx))
            return null;

        var coordinator = _userService.GetById(coordinatorUserId);
        if (coordinator is null || !coordinator.IsCoordinator)
            return null;

        lock (_batchLock)
        {
            SyncResult result = tx.Type?.ToLowerInvariant() switch
            {
                "checkout" => ForceApplyCheckout(tx),
                "return" => ForceApplyReturn(tx),
                "damage_flag" => ProcessDamageFlag(tx),
                _ => new SyncResult
                {
                    DeviceTransactionId = deviceTransactionId,
                    Status = "error",
                    ConflictDetails = $"Unknown transaction type '{tx.Type}'."
                }
            };

            _overrideAuditLog.Add(new ConflictOverrideAuditEntry
            {
                DeviceTransactionId = deviceTransactionId,
                CoordinatorUserId = coordinatorUserId,
                CoordinatorName = coordinator.Username,
                OverrideReason = overrideReason,
                AppliedAtUtc = DateTime.UtcNow
            });

            _results[deviceTransactionId] = result;
            return result;
        }
    }

    public IReadOnlyList<ConflictOverrideAuditEntry> GetOverrideAuditLog()
        => _overrideAuditLog.AsReadOnly();

    private SyncResult ProcessSingle(OfflineSyncTransaction tx, int requestingUserId)
    {
        if (_results.TryGetValue(tx.DeviceTransactionId, out var cached))
            return cached;

        if (tx.BorrowerUserId != requestingUserId)
        {
            var requestingUser = _userService.GetById(requestingUserId);
            if (requestingUser is null || !requestingUser.IsCoordinator)
            {
                return new SyncResult
                {
                    DeviceTransactionId = tx.DeviceTransactionId,
                    Status = "error",
                    ConflictDetails = $"BorrowerUserId {tx.BorrowerUserId} does not match authenticated user {requestingUserId}."
                };
            }
        }

        return tx.Type?.ToLowerInvariant() switch
        {
            "checkout" => ProcessCheckout(tx),
            "return" => ProcessReturn(tx),
            "damage_flag" => ProcessDamageFlag(tx),
            _ => new SyncResult
            {
                DeviceTransactionId = tx.DeviceTransactionId,
                Status = "error",
                ConflictDetails = $"Unknown transaction type '{tx.Type}'."
            }
        };
    }

    private SyncResult ProcessCheckout(OfflineSyncTransaction tx)
    {
        var item = _equipmentService.GetItem(tx.ItemId);
        if (item is null)
        {
            return new SyncResult
            {
                DeviceTransactionId = tx.DeviceTransactionId,
                Status = "error",
                ConflictDetails = $"Equipment item {tx.ItemId} not found."
            };
        }

        if (!item.IsAvailable)
        {
            var activeRecord = _equipmentService.GetActiveCheckoutRecord(tx.ItemId);
            bool sameUser = activeRecord?.BorrowerUserId == tx.BorrowerUserId;

            if (!sameUser)
            {
                bool applyLwwOverride = activeRecord?.OfflineTimestamp.HasValue == true &&
                                        tx.OfflineTimestamp > activeRecord.OfflineTimestamp.Value;

                if (applyLwwOverride)
                {
                    _equipmentService.Return(tx.ItemId);

                    var borrower = _userService.GetById(tx.BorrowerUserId);
                    string borrowerName = borrower?.Username ?? $"User#{tx.BorrowerUserId}";
                    bool ok = _equipmentService.Checkout(tx.ItemId, borrowerName, tx.BorrowerUserId);
                    if (!ok)
                    {
                        return new SyncResult
                        {
                            DeviceTransactionId = tx.DeviceTransactionId,
                            Status = "error",
                            ConflictDetails = $"LWW checkout failed for item {tx.ItemId} after force-return."
                        };
                    }

                    var serverReceivedAt = DateTime.UtcNow;
                    var newRecord = _equipmentService.GetActiveCheckoutRecord(tx.ItemId);
                    if (newRecord is not null)
                    {
                        newRecord.OfflineTimestamp = tx.OfflineTimestamp;
                        if (!string.IsNullOrEmpty(tx.BatchTransactionId))
                            newRecord.BatchTransactionId = tx.BatchTransactionId;
                        newRecord.ServerReceivedAt = serverReceivedAt;
                    }

                    string overrideMsg =
                        $"LWW override for item '{item.Name}' (ID {item.Id}): transaction {tx.DeviceTransactionId} " +
                        $"from user {tx.BorrowerUserId} replaced an earlier offline checkout.";
                    NotifyCoordinators(newRecord?.Id ?? 0, overrideMsg);

                    return new SyncResult
                    {
                        DeviceTransactionId = tx.DeviceTransactionId,
                        Status = "success",
                        ServerReceivedAt = serverReceivedAt
                    };
                }

                string holderName = activeRecord?.BorrowerName ?? "(unknown)";
                string conflictMsg =
                    $"Offline checkout conflict for item '{item.Name}' (ID {item.Id}): device transaction " +
                    $"{tx.DeviceTransactionId} from user {tx.BorrowerUserId} was rejected because the item is " +
                    $"already checked out by '{holderName}'. Offline timestamp: {tx.OfflineTimestamp:O}.";
                string plainLanguageMessage = "This item was checked out by someone else — your action was not applied.";
                string workerMessage =
                    $"Your checkout of '{item.Name}' was not applied — a more recent record was already recorded. " +
                    "Your coordinator has been notified and can manually override if needed.";

                NotifyCoordinators(activeRecord?.Id ?? 0, conflictMsg);
                return new SyncResult
                {
                    DeviceTransactionId = tx.DeviceTransactionId,
                    Status = "conflict",
                    ConflictDetails = conflictMsg,
                    PlainLanguageMessage = plainLanguageMessage,
                    WorkerMessage = workerMessage
                };
            }

            return new SyncResult
            {
                DeviceTransactionId = tx.DeviceTransactionId,
                Status = "success"
            };
        }

        return CompleteCheckout(tx);
    }

    private SyncResult ProcessReturn(OfflineSyncTransaction tx)
    {
        var item = _equipmentService.GetItem(tx.ItemId);
        if (item is null)
        {
            return new SyncResult
            {
                DeviceTransactionId = tx.DeviceTransactionId,
                Status = "error",
                ConflictDetails = $"Equipment item {tx.ItemId} not found."
            };
        }

        if (item.IsAvailable)
        {
            return new SyncResult
            {
                DeviceTransactionId = tx.DeviceTransactionId,
                Status = "success"
            };
        }

        var serverReceivedAt = DateTime.UtcNow;
        var activeRecord = _equipmentService.GetActiveCheckoutRecord(tx.ItemId);
        if (activeRecord is not null)
        {
            if (!string.IsNullOrEmpty(tx.BatchTransactionId))
                activeRecord.BatchTransactionId = tx.BatchTransactionId;
            if (!string.IsNullOrEmpty(tx.ConditionNotes))
                activeRecord.ReturnConditionNote = tx.ConditionNotes;
            activeRecord.ServerReceivedAt = serverReceivedAt;
        }

        bool ok = _equipmentService.Return(tx.ItemId, tx.ConditionNotes);
        if (!ok)
        {
            return new SyncResult
            {
                DeviceTransactionId = tx.DeviceTransactionId,
                Status = "error",
                ConflictDetails = $"Return failed for item {tx.ItemId}."
            };
        }

        return new SyncResult
        {
            DeviceTransactionId = tx.DeviceTransactionId,
            Status = "success",
            ServerReceivedAt = serverReceivedAt
        };
    }

    private SyncResult ProcessDamageFlag(OfflineSyncTransaction tx)
    {
        var item = _equipmentService.GetItem(tx.ItemId);
        if (item is null)
        {
            return new SyncResult
            {
                DeviceTransactionId = tx.DeviceTransactionId,
                Status = "error",
                ConflictDetails = $"Equipment item {tx.ItemId} not found."
            };
        }

        string description = tx.Description ?? "(no description provided)";
        var flag = _equipmentService.FlagDamage(
            tx.ItemId,
            description,
            tx.BorrowerUserId,
            tx.DeviceTransactionId,
            tx.OfflineTimestamp);

        if (flag is null)
        {
            return new SyncResult
            {
                DeviceTransactionId = tx.DeviceTransactionId,
                Status = "error",
                ConflictDetails = $"Failed to create damage flag for item {tx.ItemId}."
            };
        }

        string notificationMessage =
            $"Damage flag submitted for item '{item.Name}' (ID {item.Id}) by user {tx.BorrowerUserId}. " +
            $"Description: {description}. Device timestamp: {tx.OfflineTimestamp:O}. " +
            $"Server received: {flag.ServerReceivedAt:O}.";
        NotifyCoordinators(0, notificationMessage);

        return new SyncResult
        {
            DeviceTransactionId = tx.DeviceTransactionId,
            Status = "success",
            ServerReceivedAt = flag.ServerReceivedAt
        };
    }

    private SyncResult CompleteCheckout(OfflineSyncTransaction tx)
    {
        var borrowerUser = _userService.GetById(tx.BorrowerUserId);
        string borrowerName = borrowerUser?.Username ?? $"User#{tx.BorrowerUserId}";

        bool ok = _equipmentService.Checkout(tx.ItemId, borrowerName, tx.BorrowerUserId);
        if (!ok)
        {
            return new SyncResult
            {
                DeviceTransactionId = tx.DeviceTransactionId,
                Status = "error",
                ConflictDetails = $"Checkout failed for item {tx.ItemId} (concurrent modification)."
            };
        }

        var serverReceivedAt = DateTime.UtcNow;
        var newRecord = _equipmentService.GetActiveCheckoutRecord(tx.ItemId);
        if (newRecord is not null)
        {
            newRecord.OfflineTimestamp = tx.OfflineTimestamp;
            if (!string.IsNullOrEmpty(tx.BatchTransactionId))
                newRecord.BatchTransactionId = tx.BatchTransactionId;
            newRecord.ServerReceivedAt = serverReceivedAt;
        }

        return new SyncResult
        {
            DeviceTransactionId = tx.DeviceTransactionId,
            Status = "success",
            ServerReceivedAt = serverReceivedAt
        };
    }

    private SyncResult ForceApplyCheckout(OfflineSyncTransaction tx)
    {
        var item = _equipmentService.GetItem(tx.ItemId);
        if (item is not null && !item.IsAvailable)
            _equipmentService.Return(tx.ItemId);

        return CompleteCheckout(tx);
    }

    private SyncResult ForceApplyReturn(OfflineSyncTransaction tx)
    {
        return ProcessReturn(tx);
    }

    private void NotifyCoordinators(int recordId, string message)
    {
        foreach (var coordinator in _userService.GetCoordinators())
            _notificationService.CreateNotification(coordinator.Id, recordId, message);
    }
}

/// <summary>Immutable audit log entry for a coordinator conflict override.</summary>
public class ConflictOverrideAuditEntry
{
    public string DeviceTransactionId { get; init; } = string.Empty;
    public int CoordinatorUserId { get; init; }
    public string CoordinatorName { get; init; } = string.Empty;
    public string OverrideReason { get; init; } = string.Empty;
    public DateTime AppliedAtUtc { get; init; }
}
