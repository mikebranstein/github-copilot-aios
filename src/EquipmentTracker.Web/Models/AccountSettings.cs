namespace EquipmentTracker.Web.Models;

// Added for Issue #117 - Approval Workflow for Restricted Equipment Checkout

/// <summary>
/// Account-wide configuration for the restricted equipment approval workflow.
/// Per the BA constraints, timeout configuration is global for MVP
/// (per-equipment-type configuration is explicitly deferred to post-MVP).
/// </summary>
public class AccountSettings
{
    /// <summary>
    /// Minutes before an unanswered approval request is escalated to the delegate approver.
    /// Default: 15 minutes (field-validated SLA benchmark).
    /// </summary>
    public int ApprovalTimeoutMinutes { get; set; } = 15;

    /// <summary>
    /// User ID of the configured delegate/backup approver to receive escalated requests.
    /// Required for AC-7 escalation path; if null, escalation records the event but no push is sent.
    /// </summary>
    public int? DelegateApproverId { get; set; }
}
