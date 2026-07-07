using System.Collections.Concurrent;
using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Singleton service that processes batches of offline transactions from mobile clients.
///
/// Idempotency note (v1): Processed transaction IDs are stored in-memory in a
/// ConcurrentDictionary. This resets on app restart. After restart, duplicate syncs
/// from the same offline batch are handled by EquipmentService.Checkout() idempotency
/// check (IsIdempotentCheckout within 60 s) as a secondary guard. This is acceptable for v1.
///
/// Thread safety: ProcessBatch uses a lock to prevent concurrent batches from racing
/// on the same equipment items. ConcurrentDictionary is used for the results store.
/// </summary>
public class OfflineSyncService : IOfflineSyncService
{
    private readonly IEquipmentService _equipmentService;
    private readonly ICoordinatorNotificationService _notificationService;
    private readonly IUserService _userService;

    // In-memory store of all processed transaction results (survives the lifetime of the process).
    // Key: DeviceTransactionId
    private readonly ConcurrentDictionary<string, SyncResult> _results = new(StringComparer.OrdinalIgnoreCase);

    // Serialises concurrent batch submissions to prevent data races.
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

    /// <inheritdoc/>
    public IReadOnlyList<SyncResult> ProcessBatch(
        IReadOnlyList<OfflineSyncTransaction> transactions,
        int requestingUserId)
    {
        if (transactions is null || transactions.Count == 0)
            return Array.Empty<SyncResult>();

        // Sort chronologically before acquiring the lock so the sort work happens outside the
        // critical section (though for ≤50 items the difference is negligible — AC7).
        var ordered = transactions.OrderBy(t => t.OfflineTimestamp).ToList();

        var batchResults = new List<SyncResult>(ordered.Count);

        lock (_batchLock)
        {
            foreach (var tx in ordered)
            {
                var result = ProcessSingle(tx, requestingUserId);
                // Store result for future GetResult() look-ups.
                _results[tx.DeviceTransactionId] = result;
                batchResults.Add(result);
            }
        }

        return batchResults.AsReadOnly();
    }

    /// <inheritdoc/>
    public SyncResult? GetResult(string deviceTransactionId)
    {
        _results.TryGetValue(deviceTransactionId, out var result);
        return result;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private SyncResult ProcessSingle(OfflineSyncTransaction tx, int requestingUserId)
    {
        // Idempotency check — if already processed, return stored result.
        if (_results.TryGetValue(tx.DeviceTransactionId, out var cached))
            return cached;

        // Risk 4 (MEDIUM): BorrowerUserId validation.
        // Non-coordinator users may only submit transactions for themselves.
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
            "return"   => ProcessReturn(tx),
            _          => new SyncResult
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

        // Conflict detection: item already checked out by a different user.
        if (!item.IsAvailable)
        {
            var activeRecord = _equipmentService.GetActiveCheckoutRecord(tx.ItemId);
            bool sameUser = activeRecord?.BorrowerUserId == tx.BorrowerUserId;

            if (!sameUser)
            {
                // AC4 / AC8: create coordinator notification and void this transaction.
                string holderName = activeRecord?.BorrowerName ?? "(unknown)";
                string conflictMsg =
                    $"Offline checkout conflict for item '{item.Name}' (ID {item.Id}): " +
                    $"device transaction {tx.DeviceTransactionId} from user {tx.BorrowerUserId} was rejected " +
                    $"because the item is already checked out by '{holderName}'. " +
                    $"Offline timestamp: {tx.OfflineTimestamp:O}.";

                // Notify all coordinators.
                var coordinators = _userService.GetCoordinators();
                int checkoutRecordId = activeRecord?.Id ?? 0;
                foreach (var coord in coordinators)
                {
                    _notificationService.CreateNotification(coord.Id, checkoutRecordId, conflictMsg);
                }

                return new SyncResult
                {
                    DeviceTransactionId = tx.DeviceTransactionId,
                    Status = "conflict",
                    ConflictDetails = conflictMsg
                };
            }

            // Same user: idempotent — treat as success without double-checking out.
            return new SyncResult
            {
                DeviceTransactionId = tx.DeviceTransactionId,
                Status = "success"
            };
        }

        // Perform the checkout.
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

        // Risk 3 (MEDIUM): set OfflineTimestamp on the resulting CheckoutRecord.
        var newRecord = _equipmentService.GetActiveCheckoutRecord(tx.ItemId);
        if (newRecord is not null)
        {
            newRecord.OfflineTimestamp = tx.OfflineTimestamp;
            // Issue #114: propagate BatchTransactionId from offline bulk transaction
            if (!string.IsNullOrEmpty(tx.BatchTransactionId))
                newRecord.BatchTransactionId = tx.BatchTransactionId;
        }

        return new SyncResult
        {
            DeviceTransactionId = tx.DeviceTransactionId,
            Status = "success"
        };
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
            // Already returned — idempotent success.
            return new SyncResult
            {
                DeviceTransactionId = tx.DeviceTransactionId,
                Status = "success"
            };
        }

        // Issue #114: stamp BatchTransactionId on the active checkout record before return
        var activeReturnRecord = _equipmentService.GetActiveCheckoutRecord(tx.ItemId);
        if (activeReturnRecord is not null && !string.IsNullOrEmpty(tx.BatchTransactionId))
            activeReturnRecord.BatchTransactionId = tx.BatchTransactionId;

        bool ok = _equipmentService.Return(tx.ItemId);
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
            Status = "success"
        };
    }
}
