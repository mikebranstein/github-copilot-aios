namespace EquipmentTracker.Web.Models;

/// <summary>
/// Represents a single item in a field bulk checkout or return cart.
/// Added for Issue #114 — Bulk Checkout and Return Operations for Field Teams.
/// </summary>
public class BulkCartItem
{
    public int EquipmentItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// True when the item is already checked out by another user at the time it was scanned into the cart.
    /// The Field Manager may choose to Skip or Keep conflicted items before confirming.
    /// </summary>
    public bool HasConflict { get; set; }

    /// <summary>
    /// Name of the user currently holding the item (populated when HasConflict = true).
    /// </summary>
    public string? ConflictHolderName { get; set; }

    /// <summary>
    /// Field Manager chose to skip this conflicted item — it will be excluded from the bulk checkout.
    /// </summary>
    public bool IsSkipped { get; set; }
}
