using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Manages soft holds for equipment availability.
/// Enforces first-write-wins concurrency: only one active hold per item is permitted.
/// Added for Issue #118 — Real-Time Equipment Availability Dashboard.
/// </summary>
public interface ISoftHoldService
{
    /// <summary>
    /// Attempts to place a 30-minute soft hold on the given item for the given user.
    /// Returns the created hold if successful, or null if the item already has an active hold (race condition — caller should notify the losing user).
    /// </summary>
    Task<SoftHold?> PlaceHoldAsync(int equipmentItemId, int userId);

    /// <summary>
    /// Releases a hold early. Only the owning user may release their own hold.
    /// Returns true if the hold was found and released; false if not found, already released, or belongs to another user.
    /// </summary>
    Task<bool> ReleaseHoldAsync(int holdId, int userId);

    /// <summary>
    /// Returns the current active hold for the given equipment item, or null if no active hold exists.
    /// </summary>
    SoftHold? GetActiveHold(int equipmentItemId);

    /// <summary>
    /// Returns all holds (active, released, and expired) for audit/history purposes.
    /// </summary>
    IReadOnlyList<SoftHold> GetHoldsForItem(int equipmentItemId);

    /// <summary>
    /// Expires all holds whose ExpiresAtUtc has passed (called by the background job).
    /// Returns the list of item IDs whose holds were expired (so Notify Me subscriptions can be triggered).
    /// </summary>
    Task<IReadOnlyList<int>> ExpireStaleHoldsAsync();
}
