namespace EquipmentTracker.Web.Models;

/// <summary>
/// Scheduled maintenance downtime for a specific asset.
/// Used in utilization calculation: available hours = calendar hours – maintenance hours.
/// Added for Issue #122.
/// </summary>
public class MaintenanceDowntimeRecord
{
    public int Id { get; set; }
    public int EquipmentItemId { get; set; }
    public DateTime DowntimeStart { get; set; }
    public DateTime DowntimeEnd { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
