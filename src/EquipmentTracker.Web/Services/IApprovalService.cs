using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public interface IApprovalService
{
    /// <summary>Creates a new Pending approval request for the given checkout record.</summary>
    ApprovalRequest CreateRequest(int checkoutRecordId, int requestingUserId);

    /// <summary>Approves a pending request. Returns false if not in Pending state.</summary>
    bool Approve(int requestId, int coordinatorId);

    /// <summary>Denies a pending request. Returns false if not in Pending state.</summary>
    bool Deny(int requestId, int coordinatorId, string? reason);

    /// <summary>Auto-approves all Pending requests older than the given timeout.</summary>
    void AutoApproveExpired(TimeSpan timeout);

    /// <summary>Returns all Pending approval requests.</summary>
    IReadOnlyList<ApprovalRequest> GetPending();

    /// <summary>Returns all approval requests (for audit).</summary>
    IReadOnlyList<ApprovalRequest> GetAll();

    /// <summary>Returns the approval request for the given checkout record, or null.</summary>
    ApprovalRequest? GetByCheckoutRecordId(int checkoutRecordId);

    // Added for Issue #117 - Approval Workflow for Restricted Equipment Checkout

    /// <summary>
    /// Creates a restricted checkout approval request and sends a push notification to the designated approver.
    /// The checkout is placed in Pending state; equipment is not released until approved.
    /// Implements AC-2 (blocked pending approval) and AC-3 (push notification within 2-minute SLA).
    /// </summary>
    Task<ApprovalRequest> CreateRestrictedRequestAsync(
        int checkoutRecordId,
        int requestingUserId,
        int equipmentItemId,
        int? approverUserId,
        int? delegateApproverId,
        string equipmentName,
        string requestorName,
        string? checkoutDuration = null);

    /// <summary>
    /// Escalates all Pending requests older than the configured timeout to the delegate approver.
    /// Sends push notifications to both primary and delegate approvers.
    /// Writes an Escalated audit log entry. Implements AC-7.
    /// </summary>
    Task EscalateTimedOutAsync(int timeoutMinutes);

    /// <summary>
    /// Invokes an emergency override for the given approval request.
    /// Only callable by users with the Safety Admin role.
    /// Checkout proceeds immediately; an immutable audit log entry with EmergencyOverrideFlag=true is written.
    /// Implements AC-8. Returns false if the request is not found or reason is empty.
    /// </summary>
    bool EmergencyOverride(int requestId, int safetyAdminUserId, string reason);

    /// <summary>Returns the append-only restricted audit log (all entries, newest-first).</summary>
    IReadOnlyList<RestrictedAuditLogEntry> GetAuditLog();

    /// <summary>Returns audit log entries for a specific equipment item.</summary>
    IReadOnlyList<RestrictedAuditLogEntry> GetAuditLogForEquipment(int equipmentItemId);
}
