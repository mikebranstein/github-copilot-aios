namespace EquipmentTracker.Web.Models;

/// <summary>
/// Persistent in-memory checkout or return cart for a single Field Manager session.
/// Keyed by userId. Added for Issue #114.
/// </summary>
public class BulkCart
{
    /// <summary>Maximum items allowed in a single bulk transaction (AC2 constraint).</summary>
    public const int MaxItems = 50;

    public int OwnerUserId { get; set; }

    /// <summary>"checkout" or "return"</summary>
    public string CartType { get; set; } = "checkout";

    public List<BulkCartItem> Items { get; set; } = new();

    /// <summary>UTC timestamp of the last scan (used to detect stale carts).</summary>
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;

    public int ItemCount => Items.Count;

    /// <summary>
    /// Returns true if the item is already in the cart (prevents duplicate scans).
    /// </summary>
    public bool ContainsItem(int equipmentItemId) =>
        Items.Any(i => i.EquipmentItemId == equipmentItemId);
}
