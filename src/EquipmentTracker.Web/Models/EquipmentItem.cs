namespace EquipmentTracker.Web.Models;

public class EquipmentItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Available;
    public int? SiteId { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Added for Issue #115 - Equipment Condition Assessment & Damage Tracking at Return
    public EquipmentLifecycleStatus LifecycleStatus { get; set; } = EquipmentLifecycleStatus.Available;
}