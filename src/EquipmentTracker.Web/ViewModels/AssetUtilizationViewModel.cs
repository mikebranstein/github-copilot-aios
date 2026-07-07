using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>
/// ViewModel for the per-asset utilization detail view (AC-1).
/// </summary>
public class AssetUtilizationViewModel
{
    public AssetUtilizationMetrics? Metrics { get; set; }

    /// <summary>
    /// Utilization calculation methodology (AC-8 tooltip text).
    /// Active hours = sum of checkout durations within the period.
    /// Available hours = calendar hours − scheduled maintenance hours.
    /// Utilization % = Active hours ÷ Available hours.
    /// </summary>
    public string MethodologyTooltip { get; set; } =
        "Utilization % = Active Hours ÷ Available Hours. " +
        "Active Hours = total time equipment was checked out in the period. " +
        "Available Hours = Calendar Hours − Scheduled Maintenance Hours.";
}
