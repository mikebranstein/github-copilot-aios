namespace EquipmentTracker.Web.Models;

public class CheckoutRecord
{
    public int Id { get; set; }
    public int EquipmentItemId { get; set; }
    public string BorrowerName { get; set; } = string.Empty;
    public int? BorrowerUserId { get; set; }
    public DateTime CheckedOutAtUtc { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }
    public int? JobSiteId { get; set; }
    public string? ConditionNote { get; set; }
    public string? ReturnConditionNote { get; set; }
    public int? BulkCheckoutInitiatorId { get; set; }
    public bool IsVoided { get; set; }
    public DateTime? OfflineTimestamp { get; set; }
    public string? BatchTransactionId { get; set; }
    public string? ConditionPhotoAtCheckout { get; set; }
    public string? ConditionAtCheckout { get; set; }
    public bool ConditionPhotoSkippedAtCheckout { get; set; }
    public string? ConditionPhotoAtReturn { get; set; }
    public string? ConditionAtReturn { get; set; }
    public string? ConditionAssessment { get; set; }
    public CertValidationOutcome CertValidationResult { get; set; } = CertValidationOutcome.NotRequired;
    public int? OverrideRecordId { get; set; }
    public int? ConditionRecordId { get; set; }
    public ReturnState? ReturnFlowState { get; set; }
    public bool IsPendingApproval { get; set; }

    // Added for Issue #121 — Offline-First Mobile App for Field Workers
    /// <summary>
    /// UTC timestamp when the server processed this checkout during offline sync.
    /// For OSHA dual-timestamp compliance: OfflineTimestamp records when the action was taken
    /// offline (device-side); ServerReceivedAt records when the server received it during sync.
    /// Null for checkouts not created via offline sync.
    /// </summary>
    public DateTime? ServerReceivedAt { get; set; }
}
