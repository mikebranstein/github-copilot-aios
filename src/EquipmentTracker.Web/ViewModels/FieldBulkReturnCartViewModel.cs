using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>
/// ViewModel for the Field Manager Bulk Return scan-to-cart screen.
/// Added for Issue #114 — Bulk Checkout and Return Operations for Field Teams.
/// </summary>
public class FieldBulkReturnCartViewModel
{
    public BulkCart Cart { get; set; } = new();

    /// <summary>Error message displayed when a scan fails.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Inline scan result message for the most recently scanned item.</summary>
    public string? ScanFeedback { get; set; }

    public int CartItemCount => Cart.ItemCount;
}
