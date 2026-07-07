namespace EquipmentTracker.Web.Models;

public class QueueAuditEvent
{
    public int Id { get; set; }
    public int EquipmentItemId { get; set; }
    public int? WaitlistEntryId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
