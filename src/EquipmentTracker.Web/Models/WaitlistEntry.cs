namespace EquipmentTracker.Web.Models;

public class WaitlistEntry
{
    public int Id { get; set; }
    public int EquipmentItemId { get; set; }
    public int BorrowerUserId { get; set; }
    public string BorrowerName { get; set; } = string.Empty;
    public int QueuePosition { get; set; }
    public WaitlistTier Tier { get; set; } = WaitlistTier.Standard;
    public WaitlistStatus Status { get; set; } = WaitlistStatus.Waiting;
    public DateTime JoinedAtUtc { get; set; }
    public DateTime? ReservedAtUtc { get; set; }
    public DateTime? ConfirmationDeadlineUtc { get; set; }
    public DateTime? ExitedAtUtc { get; set; }
    public string? ExitReason { get; set; }
    public string? OverrideReason { get; set; }
}

public enum WaitlistTier { Standard = 0, Urgent = 1 }
public enum WaitlistStatus { Waiting, Reserved, Fulfilled, Cancelled, Forfeited, CoordinatorRemoved }
