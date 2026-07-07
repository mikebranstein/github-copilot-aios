namespace EquipmentTracker.Web.Models;

/// <summary>
/// Result returned after confirming a bulk checkout or return operation.
/// Added for Issue #114 — Bulk Checkout and Return Operations for Field Teams.
/// </summary>
public class BulkOperationResult
{
    /// <summary>Batch ID linking all CheckoutRecords in this bulk operation.</summary>
    public string BatchTransactionId { get; set; } = string.Empty;

    /// <summary>Items successfully checked out or returned.</summary>
    public List<BulkCartItem> SucceededItems { get; set; } = new();

    /// <summary>Items that failed (e.g., conflict skipped, or error during checkout).</summary>
    public List<BulkCartItem> FailedItems { get; set; } = new();

    public int SuccessCount => SucceededItems.Count;
    public int FailedCount => FailedItems.Count;
    public int TotalAttempted => SuccessCount + FailedCount;
}
