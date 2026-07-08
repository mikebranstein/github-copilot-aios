using EquipmentTracker.Web.Services;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>Downtime Cost Calculator view model (AC-5).</summary>
public class DowntimeCostCalculatorViewModel
{
    /// <summary>Number of assets enrolled in Smart Maintenance Scheduling.</summary>
    public int AssetCount { get; set; }

    /// <summary>Industry default: $1,200–$1,700/day. User-configurable at onboarding and in settings.</summary>
    public double DailyCostEstimate { get; set; } = 1450.0;

    /// <summary>Industry default: 2–3 day avg repair. User-configurable.</summary>
    public double AvgRepairDays { get; set; } = 2.5;

    /// <summary>Estimated unplanned incidents per year. User-configurable.</summary>
    public double EstimatedIncidentsPerYear { get; set; } = 4.0;

    public DowntimeCostSummary? Result { get; set; }
}
