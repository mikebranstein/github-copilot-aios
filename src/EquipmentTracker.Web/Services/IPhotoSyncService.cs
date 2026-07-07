namespace EquipmentTracker.Web.Services;

/// <summary>
/// Manages queuing and syncing of photos captured while offline.
/// Added for Issue #58 — Photo-Backed Checkout &amp; Return.
/// </summary>
public interface IPhotoSyncService
{
    /// <summary>Queue a photo for sync when online.</summary>
    Task QueueForSyncAsync(int checkoutRecordId, string photoLocalPath, bool isReturn = false);

    /// <summary>
    /// Sync all pending photos with up to 3-retry exponential back-off.
    /// Returns the count of successfully synced items.
    /// </summary>
    Task<int> SyncPendingPhotosAsync();

    /// <summary>Whether any photos are waiting to be synced.</summary>
    bool HasPendingSync { get; }

    /// <summary>Whether the pending-sync indicator UI should be visible.</summary>
    bool PendingSyncIndicatorVisible { get; }
}
