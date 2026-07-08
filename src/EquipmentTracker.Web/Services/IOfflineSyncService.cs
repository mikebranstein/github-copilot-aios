using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public interface IOfflineSyncService
{
    /// <summary>
    /// Processes a batch of offline transactions in chronological order using last-write-wins
    /// for competing checkout transactions on the same item.
    /// Supported transaction types: "checkout", "return", and "damage_flag".
    ///
    /// Conflict handling:
    ///   - checkout: later offline timestamp wins when competing offline records exist;
    ///     server-side active checkouts still win over older offline submissions.
    ///   - damage_flag: server-authoritative; flags are always applied.
    ///   - return: server-authoritative; idempotent if already returned.
    ///
    /// Dual-timestamp support:
    ///   - SyncResult.ServerReceivedAt is set for each successfully applied transaction.
    ///   - CheckoutRecord.OfflineTimestamp and CheckoutRecord.ServerReceivedAt are both stored.
    ///   - DamageFlag.DeviceTimestamp and DamageFlag.ServerReceivedAt are both stored.
    /// </summary>
    /// <param name="transactions">Transactions from the device; they are sorted by OfflineTimestamp before processing.</param>
    /// <param name="requestingUserId">Authenticated user submitting the sync request.</param>
    IReadOnlyList<SyncResult> ProcessBatch(IReadOnlyList<OfflineSyncTransaction> transactions, int requestingUserId);

    /// <summary>
    /// Returns the stored result for a previously processed transaction, or null if unknown.
    /// </summary>
    SyncResult? GetResult(string deviceTransactionId);

    /// <summary>
    /// Coordinator manually overrides a conflict result, forcing the specified transaction to win.
    /// Creates an audit log entry.
    /// </summary>
    SyncResult? CoordinatorOverride(string deviceTransactionId, int coordinatorUserId, string overrideReason);
}
