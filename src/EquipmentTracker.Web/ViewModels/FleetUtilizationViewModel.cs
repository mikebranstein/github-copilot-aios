using EquipmentTracker.Web.Services;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>
/// ViewModel for the fleet-level utilization dashboard (AC-1, AC-2).
/// Displays all owned assets ranked by utilization with color-coded status indicators.
/// </summary>
public class FleetUtilizationViewModel
{
    public IReadOnlyList<AssetUtilizationMetrics> Assets { get; set; } = [];

    /// <summary>Whether the current user may export the CFO report.</summary>
    public bool CanExportCfoReport { get; set; }

    /// <summary>Total number of tracked assets.</summary>
    public int TotalAssets => Assets.Count;

    public int IdleCount    => Assets.Count(a => a.Status == UtilizationStatus.Idle);
    public int MonitorCount => Assets.Count(a => a.Status == UtilizationStatus.Monitor);
    public int HealthyCount => Assets.Count(a => a.Status == UtilizationStatus.Healthy);
}
