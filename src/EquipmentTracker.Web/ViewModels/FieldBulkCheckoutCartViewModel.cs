using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>
/// ViewModel for the Field Manager Bulk Checkout scan-to-cart screen.
/// Added for Issue #114 — Bulk Checkout and Return Operations for Field Teams.
/// </summary>
public class FieldBulkCheckoutCartViewModel
{
    public BulkCart Cart { get; set; } = new();

    /// <summary>Available borrowers for the assignee picker (zero keyboard input required — AC5).</summary>
    public List<Models.ApplicationUser> Borrowers { get; set; } = new();

    /// <summary>Currently selected assignee (defaults to the logged-in Field Manager).</summary>
    public int SelectedBorrowerId { get; set; }
    public string SelectedBorrowerName { get; set; } = string.Empty;

    /// <summary>Error message displayed when a scan fails (item not found, cart full, etc.).</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Inline scan result message for the most recently scanned item.</summary>
    public string? ScanFeedback { get; set; }

    public int CartItemCount => Cart.ItemCount;
    public bool HasConflicts => Cart.Items.Any(i => i.HasConflict && !i.IsSkipped);
}
