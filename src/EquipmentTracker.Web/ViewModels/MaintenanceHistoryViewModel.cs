using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>Maintenance History interface view model (AC-4).</summary>
public class MaintenanceHistoryViewModel
{
    public int AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double OperatingHours { get; set; }
    public IReadOnlyList<MaintenanceEvent> Events { get; set; } = Array.Empty<MaintenanceEvent>();
}

/// <summary>Form model for logging a new maintenance event.</summary>
public class LogMaintenanceEventViewModel
{
    public int AssetId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime EventDate { get; set; } = DateTime.Today;
    public double HoursAtService { get; set; }
    public string? TechnicianName { get; set; }
    public string? Notes { get; set; }
}
