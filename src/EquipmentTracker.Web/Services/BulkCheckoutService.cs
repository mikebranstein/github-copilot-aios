using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Coordinator-initiated bulk checkout service.
/// Each call checks out ONE item to ONE crew member, tagged with the coordinator's ID.
/// Approval requests are auto-approved immediately (coordinator is the approval authority).
/// </summary>
public class BulkCheckoutService : IBulkCheckoutService
{
    private readonly IEquipmentService _equipmentService;
    private readonly IApprovalService _approvalService;

    public BulkCheckoutService(IEquipmentService equipmentService, IApprovalService approvalService)
    {
        _equipmentService = equipmentService;
        _approvalService = approvalService;
    }

    public CheckoutRecord? BulkCheckout(
        int itemId,
        int borrowerUserId,
        string borrowerName,
        int initiatorCoordinatorId,
        string? conditionNote = null)
    {
        var success = _equipmentService.Checkout(
            itemId,
            borrowerName,
            borrowerUserId,
            conditionNote,
            initiatorCoordinatorId);

        if (!success)
            return null;

        var record = _equipmentService.GetActiveCheckoutRecord(itemId);
        if (record is null)
            return null;

        // Create approval request and immediately auto-approve it —
        // coordinator IS the approval authority for bulk checkouts.
        var approval = _approvalService.CreateRequest(record.Id, borrowerUserId);
        _approvalService.Approve(approval.Id, initiatorCoordinatorId);

        return record;
    }
}
