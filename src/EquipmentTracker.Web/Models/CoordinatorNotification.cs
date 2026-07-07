namespace EquipmentTracker.Web.Models;

public class CoordinatorNotification
{
    public int Id { get; set; }
    public int CoordinatorUserId { get; set; }
    public int CheckoutRecordId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsRead { get; set; }
    public string Message { get; set; } = string.Empty;
}
