namespace EquipmentTracker.Web.Models;

public class SentNotification
{
    public int CheckoutRecordId { get; set; }
    public NotificationType NotificationType { get; set; }
    public DateTime SentAtUtc { get; set; }
}

public enum NotificationType
{
    BorrowerOverdue,
    CoordinatorOverdue
}
