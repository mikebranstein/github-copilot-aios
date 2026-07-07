using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public interface IOfflineSyncService
{
    /// <summary>
    /// Processes a batch of offline transactions in chronological order.
    /// First-sync-wins conflict resolution: if an item is already checked out
    /// by a different user, the conflicting transaction is rejected and a
    /// CoordinatorNotification is created.
    /// </summary>
    /// <param name="transactions">Transactions from the device, will be sorted chronologically.</param>
    /// <param name="requestingUserId">Authenticated user submitting the sync request.</param>
    IReadOnlyList<SyncResult> ProcessBatch(IReadOnlyList<OfflineSyncTransaction> transactions, int requestingUserId);

    /// <summary>
    /// Returns the stored result for a previously processed transaction, or null if unknown.
    /// </summary>
    SyncResult? GetResult(string deviceTransactionId);
}
