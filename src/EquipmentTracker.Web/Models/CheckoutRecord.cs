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

    // Added for Issue #114 — Bulk Checkout and Return Operations for Field Teams
    /// <summary>
    /// Groups all CheckoutRecords that were created in the same bulk operation.
    /// Null for single-item checkouts. Format: lowercase GUID hex (32 chars).
    /// </summary>
    public string? BatchTransactionId { get; set; }

    // Added for Issue #58 — Photo-Backed Checkout & Return
    public string? ConditionPhotoAtCheckout { get; set; }  // URL/path to photo
    public string? ConditionAtCheckout { get; set; }  // text description
    public bool ConditionPhotoSkippedAtCheckout { get; set; } = false;
    public string? ConditionPhotoAtReturn { get; set; }  // URL/path to photo
    public string? ConditionAtReturn { get; set; }  // text description
    public string? ConditionAssessment { get; set; }  // NoDamage, MinorDamage, SignificantDamage (set by coordinator)

    // Added for Issue #117 — Approval Workflow for Restricted Equipment Checkout
    /// <summary>
    /// True when this checkout is awaiting approval for restricted equipment.
    /// The item is reserved (IsAvailable=false) but not yet released to the operator.
    /// Cleared to false when the approval request is approved or emergency-overridden.
    /// </summary>
    public bool IsPendingApproval { get; set; } = false;
}
