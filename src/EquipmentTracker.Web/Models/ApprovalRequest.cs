namespace EquipmentTracker.Web.Models;

public enum ApprovalStatus
{
    Pending,
    Approved,
    Denied,
    AutoApproved
}

public class ApprovalRequest
{
    public int Id { get; set; }
    public int CheckoutRecordId { get; set; }
    public int RequestingUserId { get; set; }
    public int? ApprovingCoordinatorId { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string? DenialReason { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
}
