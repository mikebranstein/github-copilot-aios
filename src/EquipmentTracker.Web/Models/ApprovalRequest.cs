namespace EquipmentTracker.Web.Models;

public enum ApprovalStatus
{
    Pending,
    Approved,
    Denied,
    AutoApproved,
    Escalated,          // Added for Issue #117: escalated to delegate approver after timeout
    EmergencyOverride   // Added for Issue #117: Safety Admin emergency bypass with audit trail
}

public class ApprovalRequest
{
    public int Id { get; set; }
    public int CheckoutRecordId { get; set; }
    public int RequestingUserId { get; set; }
    public int? ApprovingCoordinatorId { get; set; }

    // Added for Issue #117 - Approval Workflow for Restricted Equipment Checkout
    /// <summary>Designated delegate/backup approver for escalation when primary times out.</summary>
    public int? DelegateApproverId { get; set; }

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string? DenialReason { get; set; }

    // Added for Issue #117
    /// <summary>Mandatory reason for emergency override; null if not an override.</summary>
    public string? OverrideReason { get; set; }

    /// <summary>True when a Safety Admin invoked emergency override.</summary>
    public bool EmergencyOverrideFlag { get; set; } = false;

    /// <summary>ID of the Safety Admin who invoked emergency override, if applicable.</summary>
    public int? OverridingUserId { get; set; }

    public DateTime RequestedAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }

    /// <summary>Timestamp when this request was escalated to the delegate approver.</summary>
    public DateTime? EscalatedAtUtc { get; set; }
}
