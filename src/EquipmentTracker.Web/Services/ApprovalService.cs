using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-memory approval service (singleton). Manages coordinator approval workflow.
/// Extended for Issue #117: restricted equipment approval, escalation, emergency override, and OSHA audit log.
/// </summary>
public class ApprovalService : IApprovalService
{
    private readonly List<ApprovalRequest> _requests = new();
    private readonly List<RestrictedAuditLogEntry> _auditLog = new(); // Append-only; never modified after write
    private readonly IEquipmentService _equipmentService;
    private readonly IUserService _userService;
    private readonly IPushNotificationService _pushNotificationService;
    private int _nextId = 1;
    private int _nextAuditId = 1;
    private readonly object _lock = new();

    public ApprovalService(
        IEquipmentService equipmentService,
        IUserService userService,
        IPushNotificationService pushNotificationService)
    {
        _equipmentService = equipmentService;
        _userService = userService;
        _pushNotificationService = pushNotificationService;
    }

    public ApprovalRequest CreateRequest(int checkoutRecordId, int requestingUserId)
    {
        lock (_lock)
        {
            var request = new ApprovalRequest
            {
                Id = _nextId++,
                CheckoutRecordId = checkoutRecordId,
                RequestingUserId = requestingUserId,
                Status = ApprovalStatus.Pending,
                RequestedAtUtc = DateTime.UtcNow
            };
            _requests.Add(request);
            return request;
        }
    }

    public bool Approve(int requestId, int coordinatorId)
    {
        ApprovalRequest? approvedRequest = null;
        lock (_lock)
        {
            var request = _requests.FirstOrDefault(r => r.Id == requestId);
            if (request is null || request.Status != ApprovalStatus.Pending)
                return false;

            request.Status = ApprovalStatus.Approved;
            request.ApprovingCoordinatorId = coordinatorId;
            request.DecidedAtUtc = DateTime.UtcNow;

            // AC-2: Clear pending approval flag so checkout is now active
            var checkoutRecord = GetCheckoutRecord(request.CheckoutRecordId);
            if (checkoutRecord is not null)
                checkoutRecord.IsPendingApproval = false;

            // Write immutable audit log entry (AC-6)
            AppendAuditEntry(request, AuditDecision.Approved, approverId: coordinatorId);
            approvedRequest = request;
        }

        // AC-5: Notify the requestor that their approval request was approved (outside lock to avoid holding lock during I/O)
        var requestor = _userService.GetById(approvedRequest.RequestingUserId);
        if (requestor is not null)
        {
            _ = _pushNotificationService.SendAsync(requestor,
                "Checkout Request Approved",
                "Your equipment checkout request has been approved. You may proceed with the checkout.");
        }

        return true;
    }

    public bool Deny(int requestId, int coordinatorId, string? reason)
    {
        ApprovalRequest? deniedRequest = null;
        lock (_lock)
        {
            var request = _requests.FirstOrDefault(r => r.Id == requestId);
            if (request is null || request.Status != ApprovalStatus.Pending)
                return false;

            // AC-4: Denial reason is mandatory with minimum 10 characters
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
                return false;

            request.Status = ApprovalStatus.Denied;
            request.ApprovingCoordinatorId = coordinatorId;
            request.DenialReason = reason;
            request.DecidedAtUtc = DateTime.UtcNow;

            // Void the checkout record and release the item
            var checkoutRecord = GetCheckoutRecord(request.CheckoutRecordId);
            if (checkoutRecord is not null)
            {
                checkoutRecord.IsVoided = true;
                _equipmentService.Return(checkoutRecord.EquipmentItemId);
            }

            // Write immutable audit log entry (AC-6)
            AppendAuditEntry(request, AuditDecision.Denied, approverId: coordinatorId, denialReason: reason);
            deniedRequest = request;
        }

        // AC-5: Notify the requestor that their approval request was denied, including the reason (outside lock)
        var requestorForDeny = _userService.GetById(deniedRequest.RequestingUserId);
        if (requestorForDeny is not null)
        {
            _ = _pushNotificationService.SendAsync(requestorForDeny,
                "Checkout Request Denied",
                $"Your equipment checkout request was denied. Reason: {deniedRequest.DenialReason}");
        }

        return true;
    }

    public void AutoApproveExpired(TimeSpan timeout)
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow - timeout;
            var expired = _requests
                .Where(r => r.Status == ApprovalStatus.Pending && r.RequestedAtUtc <= cutoff)
                .ToList();

            foreach (var request in expired)
            {
                request.Status = ApprovalStatus.AutoApproved;
                request.DecidedAtUtc = DateTime.UtcNow;
            }
        }
    }

    public IReadOnlyList<ApprovalRequest> GetPending()
    {
        lock (_lock)
        {
            return _requests.Where(r => r.Status == ApprovalStatus.Pending).ToList().AsReadOnly();
        }
    }

    public IReadOnlyList<ApprovalRequest> GetAll()
    {
        lock (_lock)
        {
            return _requests.ToList().AsReadOnly();
        }
    }

    public ApprovalRequest? GetByCheckoutRecordId(int checkoutRecordId)
    {
        lock (_lock)
        {
            return _requests.FirstOrDefault(r => r.CheckoutRecordId == checkoutRecordId);
        }
    }

    // -------------------------------------------------------------------------
    // Issue #117: Restricted Equipment Approval Workflow
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<ApprovalRequest> CreateRestrictedRequestAsync(
        int checkoutRecordId,
        int requestingUserId,
        int equipmentItemId,
        int? approverUserId,
        int? delegateApproverId,
        string equipmentName,
        string requestorName,
        string? checkoutDuration = null)
    {
        ApprovalRequest request;
        lock (_lock)
        {
            request = new ApprovalRequest
            {
                Id = _nextId++,
                CheckoutRecordId = checkoutRecordId,
                RequestingUserId = requestingUserId,
                ApprovingCoordinatorId = approverUserId,
                DelegateApproverId = delegateApproverId,
                Status = ApprovalStatus.Pending,
                RequestedAtUtc = DateTime.UtcNow
            };
            _requests.Add(request);
        }

        // AC-3: Send push notification to designated approver within 2-minute SLA
        if (approverUserId.HasValue)
        {
            var approver = _userService.GetById(approverUserId.Value);
            if (approver is not null)
            {
                var duration = checkoutDuration ?? "not specified";
                var title = $"Approval Required: {equipmentName}";
                var body = $"{requestorName} is requesting checkout of {equipmentName} (duration: {duration}). " +
                           $"Tap to approve or deny.";
                await _pushNotificationService.SendAsync(approver, title, body);
            }
        }

        return request;
    }

    /// <inheritdoc/>
    public async Task EscalateTimedOutAsync(int timeoutMinutes)
    {
        List<ApprovalRequest> toEscalate;
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-timeoutMinutes);
            toEscalate = _requests
                .Where(r => r.Status == ApprovalStatus.Pending && r.RequestedAtUtc <= cutoff)
                .ToList();
        }

        foreach (var request in toEscalate)
        {
            lock (_lock)
            {
                // Re-check inside lock; another thread may have decided in between
                if (request.Status != ApprovalStatus.Pending) continue;

                request.Status = ApprovalStatus.Escalated;
                request.EscalatedAtUtc = DateTime.UtcNow;

                // Write immutable audit log entry (AC-7)
                AppendAuditEntry(request, AuditDecision.Escalated);
            }

            var checkoutRecord = GetCheckoutRecord(request.CheckoutRecordId);
            var equipmentItem = checkoutRecord is not null
                ? _equipmentService.GetItem(checkoutRecord.EquipmentItemId)
                : null;
            var itemName = equipmentItem?.Name ?? "equipment";

            // Notify primary approver of escalation
            if (request.ApprovingCoordinatorId.HasValue)
            {
                var primary = _userService.GetById(request.ApprovingCoordinatorId.Value);
                if (primary is not null)
                {
                    await _pushNotificationService.SendAsync(primary,
                        "Approval Escalated",
                        $"Your approval request for {itemName} was not responded to in time and has been escalated to the delegate approver.");
                }
            }

            // Notify delegate approver
            if (request.DelegateApproverId.HasValue)
            {
                var delegate_ = _userService.GetById(request.DelegateApproverId.Value);
                if (delegate_ is not null)
                {
                    await _pushNotificationService.SendAsync(delegate_,
                        $"Escalated Approval Required: {itemName}",
                        $"An approval request for {itemName} has been escalated to you after the primary approver did not respond.");
                }
            }
        }
    }

    /// <inheritdoc/>
    public bool EmergencyOverride(int requestId, int safetyAdminUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return false; // AC-8: reason is mandatory; empty submission rejected

        lock (_lock)
        {
            var request = _requests.FirstOrDefault(r => r.Id == requestId);
            if (request is null) return false;

            // Only process if request is actionable (Pending or Escalated)
            if (request.Status != ApprovalStatus.Pending && request.Status != ApprovalStatus.Escalated)
                return false;

            request.Status = ApprovalStatus.EmergencyOverride;
            request.OverridingUserId = safetyAdminUserId;
            request.OverrideReason = reason;
            request.EmergencyOverrideFlag = true;
            request.DecidedAtUtc = DateTime.UtcNow;

            // AC-8: Checkout proceeds immediately — clear pending flag
            var checkoutRecord = GetCheckoutRecord(request.CheckoutRecordId);
            if (checkoutRecord is not null)
                checkoutRecord.IsPendingApproval = false;

            // Write immutable audit log entry (AC-6, AC-8): emergency_override_flag = true
            AppendAuditEntry(request, AuditDecision.EmergencyOverride,
                approverId: safetyAdminUserId,
                overrideReason: reason,
                emergencyOverrideFlag: true,
                overridingUserId: safetyAdminUserId);

            return true;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<RestrictedAuditLogEntry> GetAuditLog()
    {
        lock (_lock)
        {
            return _auditLog.OrderByDescending(e => e.DecisionMadeAt).ToList().AsReadOnly();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<RestrictedAuditLogEntry> GetAuditLogForEquipment(int equipmentItemId)
    {
        lock (_lock)
        {
            return _auditLog
                .Where(e => e.EquipmentId == equipmentItemId)
                .OrderByDescending(e => e.DecisionMadeAt)
                .ToList()
                .AsReadOnly();
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Writes an append-only audit log entry. MUST be called inside the lock.
    /// Never modifies existing entries — only appends.
    /// </summary>
    private void AppendAuditEntry(
        ApprovalRequest request,
        AuditDecision decision,
        int? approverId = null,
        string? denialReason = null,
        string? overrideReason = null,
        bool emergencyOverrideFlag = false,
        int? overridingUserId = null)
    {
        var checkoutRecord = GetCheckoutRecord(request.CheckoutRecordId);
        var equipmentId = checkoutRecord?.EquipmentItemId ?? 0;

        var entry = new RestrictedAuditLogEntry
        {
            Id = _nextAuditId++,
            RequestorId = request.RequestingUserId,
            ApproverId = approverId,
            EquipmentId = equipmentId,
            CheckoutRequestedAt = request.RequestedAtUtc,
            DecisionMadeAt = DateTime.UtcNow,
            Decision = decision,
            DenialReason = denialReason,
            EmergencyOverrideFlag = emergencyOverrideFlag,
            OverrideReason = overrideReason,
            OverridingUserId = overridingUserId,
            ApprovalRequestId = request.Id
        };
        _auditLog.Add(entry); // Append-only: no further modification after this line
    }

    private CheckoutRecord? GetCheckoutRecord(int checkoutRecordId)
    {
        return _equipmentService.GetCheckoutRecordById(checkoutRecordId);
    }
}
