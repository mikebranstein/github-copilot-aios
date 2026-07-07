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
}
