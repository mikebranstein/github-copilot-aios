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
}
