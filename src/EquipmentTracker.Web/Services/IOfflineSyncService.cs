using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public interface IOfflineSyncService
{
    /// <summary>
    /// Processes a batch of offline transactions in chronological order.
    /// Supported transaction types: "checkout", "return", "damage_flag".
    ///
    /// Conflict resolution (first-sync-wins):
    ///   - checkout: If an item is already checked out by a different user, the conflicting
    ///     transaction is rejected with a plain-language message and a CoordinatorNotification
    ///     is created.
    ///   - damage_flag: Server-authoritative; flags are always applied (no checkout-style conflict).
    ///   - return: Server-authoritative; idempotent if already returned.
    ///
    /// OSHA dual-timestamp compliance (Issue #121):
    ///   - SyncResult.ServerReceivedAt is set to DateTime.UtcNow for each successfully processed tx.
    ///   - CheckoutRecord.OfflineTimestamp (device-side) and CheckoutRecord.ServerReceivedAt (sync)
    ///     are both stored.
    ///   - DamageFlag.DeviceTimestamp and DamageFlag.ServerReceivedAt are both stored.
    /// </summary>
    /// <param name="transactions">Transactions from the device, sorted chronologically before processing.</param>
    /// <param name="requestingUserId">Authenticated user submitting the sync request.</param>
    IReadOnlyList<SyncResult> ProcessBatch(IReadOnlyList<OfflineSyncTransaction> transactions, int requestingUserId);

    /// <summary>
    /// Returns the stored result for a previously processed transaction, or null if unknown.
    /// </summary>
    SyncResult? GetResult(string deviceTransactionId);
}
