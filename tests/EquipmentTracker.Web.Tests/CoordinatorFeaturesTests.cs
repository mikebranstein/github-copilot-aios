using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;

namespace EquipmentTracker.Web.Tests;

/// <summary>
/// Tests for Issue #40: Coordinator Approval Queue, Audit Trail, Condition Capture, Bulk Checkout.
/// </summary>
public class CoordinatorFeaturesTests
{
    // =========================================================
    // Helpers
    // =========================================================

    private static (EquipmentService equipment, ApprovalService approval) CreateServices()
    {
        var equipment = new EquipmentService();
        var approval = new ApprovalService(equipment);
        return (equipment, approval);
    }

    private static (EquipmentService equipment, ApprovalService approval, int checkoutRecordId) CheckoutWithApproval(
        string borrowerName = "Alice",
        int borrowerUserId = 42)
    {
        var (equipment, approval) = CreateServices();
        var item = equipment.GetAllItems().First(i => i.IsAvailable);
        equipment.Checkout(item.Id, borrowerName, borrowerUserId);
        var record = equipment.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);
        approval.CreateRequest(record!.Id, borrowerUserId);
        return (equipment, approval, record.Id);
    }

    // =========================================================
    // 1. ApprovalService_CreateRequest_StatusIsPending
    // =========================================================

    [Fact]
    public void ApprovalService_CreateRequest_StatusIsPending()
    {
        var (_, approval, recordId) = CheckoutWithApproval();
        var request = approval.GetByCheckoutRecordId(recordId);

        Assert.NotNull(request);
        Assert.Equal(ApprovalStatus.Pending, request!.Status);
    }

    // =========================================================
    // 2. ApprovalService_Approve_ChangesStatusToApproved
    // =========================================================

    [Fact]
    public void ApprovalService_Approve_ChangesStatusToApproved()
    {
        var (_, approval, recordId) = CheckoutWithApproval();
        var request = approval.GetByCheckoutRecordId(recordId);
        Assert.NotNull(request);

        var result = approval.Approve(request!.Id, coordinatorId: 1);

        Assert.True(result);
        Assert.Equal(ApprovalStatus.Approved, request.Status);
        Assert.Equal(1, request.ApprovingCoordinatorId);
        Assert.NotNull(request.DecidedAtUtc);
    }

    // =========================================================
    // 3. ApprovalService_Deny_ChangesStatusToDenied_AndVoidsCheckout
    // =========================================================

    [Fact]
    public void ApprovalService_Deny_ChangesStatusToDenied_AndVoidsCheckout()
    {
        var (equipment, approval, recordId) = CheckoutWithApproval();
        var request = approval.GetByCheckoutRecordId(recordId);
        Assert.NotNull(request);

        var result = approval.Deny(request!.Id, coordinatorId: 1, reason: "Unauthorised use");

        Assert.True(result);
        Assert.Equal(ApprovalStatus.Denied, request.Status);
        Assert.Equal("Unauthorised use", request.DenialReason);
        Assert.NotNull(request.DecidedAtUtc);

        // Checkout record should be voided
        var record = equipment.GetCheckoutRecordById(recordId);
        Assert.NotNull(record);
        Assert.True(record!.IsVoided);

        // Item should be returned to inventory
        var item = equipment.GetItem(record.EquipmentItemId);
        Assert.NotNull(item);
        Assert.True(item!.IsAvailable);
    }

    // =========================================================
    // 4. ApprovalService_AutoApproveExpired_ApprovesOldPendingRequests
    // =========================================================

    [Fact]
    public void ApprovalService_AutoApproveExpired_ApprovesOldPendingRequests()
    {
        var (_, approval, recordId) = CheckoutWithApproval();
        var request = approval.GetByCheckoutRecordId(recordId);
        Assert.NotNull(request);

        // Age the request beyond the timeout
        request!.RequestedAtUtc = DateTime.UtcNow.AddMinutes(-10);

        approval.AutoApproveExpired(TimeSpan.FromMinutes(5));

        Assert.Equal(ApprovalStatus.AutoApproved, request.Status);
        Assert.NotNull(request.DecidedAtUtc);
    }

    // =========================================================
    // 5. ApprovalService_AutoApproveExpired_DoesNotApproveRecentRequests
    // =========================================================

    [Fact]
    public void ApprovalService_AutoApproveExpired_DoesNotApproveRecentRequests()
    {
        var (_, approval, recordId) = CheckoutWithApproval();
        var request = approval.GetByCheckoutRecordId(recordId);
        Assert.NotNull(request);

        // Request is recent — just created
        approval.AutoApproveExpired(TimeSpan.FromMinutes(5));

        Assert.Equal(ApprovalStatus.Pending, request!.Status);
    }

    // =========================================================
    // 6. ApprovalService_DoubleApprove_ReturnsFalseSecondTime
    // =========================================================

    [Fact]
    public void ApprovalService_DoubleApprove_ReturnsFalseSecondTime()
    {
        var (_, approval, recordId) = CheckoutWithApproval();
        var request = approval.GetByCheckoutRecordId(recordId);
        Assert.NotNull(request);

        var first = approval.Approve(request!.Id, coordinatorId: 1);
        var second = approval.Approve(request.Id, coordinatorId: 2);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, request.ApprovingCoordinatorId); // First coordinator unchanged
    }

    // =========================================================
    // 7. AuditExportService_GenerateCsv_ContainsExpectedColumns
    // =========================================================

    [Fact]
    public void AuditExportService_GenerateCsv_ContainsExpectedColumns()
    {
        var (equipment, approval) = CreateServices();
        var auditService = new AuditExportService(equipment, approval);

        var csv = auditService.GenerateCsv(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        Assert.Contains("EquipmentId", csv);
        Assert.Contains("ItemName", csv);
        Assert.Contains("CheckedOutBy", csv);
        Assert.Contains("CheckoutTimestamp", csv);
        Assert.Contains("ReturnTimestamp", csv);
        Assert.Contains("ApprovalStatus", csv);
        Assert.Contains("DenialReason", csv);
        Assert.Contains("ConditionNote", csv);
        Assert.Contains("ReturnConditionNote", csv);
    }

    // =========================================================
    // 8. AuditExportService_GenerateCsv_ThrowsForRangeOver90Days
    // =========================================================

    [Fact]
    public void AuditExportService_GenerateCsv_ThrowsForRangeOver90Days()
    {
        var (equipment, approval) = CreateServices();
        var auditService = new AuditExportService(equipment, approval);

        var ex = Assert.Throws<ArgumentException>(() =>
            auditService.GenerateCsv(DateTime.UtcNow.AddDays(-91), DateTime.UtcNow));

        Assert.Contains("90 days", ex.Message);
    }

    // =========================================================
    // 9. AuditExportService_GenerateCsv_IncludesApprovalStatus
    // =========================================================

    [Fact]
    public void AuditExportService_GenerateCsv_IncludesApprovalStatus()
    {
        var (equipment, approval) = CreateServices();
        var item = equipment.GetAllItems().First(i => i.IsAvailable);
        equipment.Checkout(item.Id, "Bob", borrowerUserId: 10);
        var record = equipment.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);
        var req = approval.CreateRequest(record!.Id, requestingUserId: 10);
        approval.Approve(req.Id, coordinatorId: 1);

        var auditService = new AuditExportService(equipment, approval);
        var csv = auditService.GenerateCsv(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        Assert.Contains("Approved", csv);
    }

    // =========================================================
    // 10. ConditionNote_StoredOnCheckoutRecord
    // =========================================================

    [Fact]
    public void ConditionNote_StoredOnCheckoutRecord()
    {
        var equipment = new EquipmentService();
        var item = equipment.GetAllItems().First(i => i.IsAvailable);

        equipment.Checkout(item.Id, "Carol", borrowerUserId: 20, conditionNote: "Minor scratch on lid");

        var record = equipment.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);
        Assert.Equal("Minor scratch on lid", record!.ConditionNote);
    }

    // =========================================================
    // 11. ReturnConditionNote_StoredOnReturnRecord
    // =========================================================

    [Fact]
    public void ReturnConditionNote_StoredOnReturnRecord()
    {
        var equipment = new EquipmentService();
        var item = equipment.GetAllItems().First(i => i.IsAvailable);
        equipment.Checkout(item.Id, "Dave", borrowerUserId: 21);
        var checkoutRecord = equipment.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(checkoutRecord);

        equipment.Return(item.Id, returnConditionNote: "Returned in good condition");

        // Record is still accessible via history
        var history = equipment.GetCheckoutHistory(item.Id);
        var returned = history.FirstOrDefault(r => r.Id == checkoutRecord!.Id);
        Assert.NotNull(returned);
        Assert.Equal("Returned in good condition", returned!.ReturnConditionNote);
        Assert.NotNull(returned.ReturnedAtUtc);
    }

    // =========================================================
    // 12. BulkCheckoutService_CreatesRecordWithBulkInitiatorId
    // =========================================================

    [Fact]
    public void BulkCheckoutService_CreatesRecordWithBulkInitiatorId()
    {
        var equipment = new EquipmentService();
        var approval = new ApprovalService(equipment);
        var bulkService = new BulkCheckoutService(equipment, approval);

        var item = equipment.GetAllItems().First(i => i.IsAvailable);
        var record = bulkService.BulkCheckout(
            itemId: item.Id,
            borrowerUserId: 99,
            borrowerName: "Eve",
            initiatorCoordinatorId: 5,
            conditionNote: "New item");

        Assert.NotNull(record);
        Assert.Equal(5, record!.BulkCheckoutInitiatorId);
        Assert.Equal("Eve", record.BorrowerName);

        // Approval request should be auto-approved
        var approvalRequest = approval.GetByCheckoutRecordId(record.Id);
        Assert.NotNull(approvalRequest);
        Assert.Equal(ApprovalStatus.Approved, approvalRequest!.Status);
        Assert.Equal(5, approvalRequest.ApprovingCoordinatorId);
    }
}
