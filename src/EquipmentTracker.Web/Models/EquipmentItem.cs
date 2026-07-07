namespace EquipmentTracker.Web.Models;

public class EquipmentItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Available;
    public int? SiteId { get; set; }
    public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
