namespace EquipmentTracker.Web.Models;

public class CheckoutRecord
{
    public int Id { get; set; }
    public int EquipmentItemId { get; set; }
    public string BorrowerName { get; set; } = string.Empty;
    public int? BorrowerUserId { get; set; }
    public DateTime CheckedOutAtUtc { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }
    public int? JobSiteId { get; set; }  // reserved for v1.1

    // Added for Issue #40
    public string? ConditionNote { get; set; }
    public string? ReturnConditionNote { get; set; }
    public int? BulkCheckoutInitiatorId { get; set; }
    public bool IsVoided { get; set; } = false;

    // Added for Issue #41 — Offline-First Mobile Checkout
    // When syncing an offline transaction, this is set to the timestamp recorded by the device.
    public DateTime? OfflineTimestamp { get; set; }
}
