using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Utilization metrics for a single asset over a specific period.
/// </summary>
public record AssetUtilizationMetrics
{
    public int AssetId { get; init; }
    public string AssetName { get; init; } = string.Empty;
    public string AssetCategory { get; init; } = string.Empty;

    /// <summary>Utilization rate for the current calendar month (0–1).</summary>
    public double CurrentMonthRate { get; init; }

    /// <summary>Utilization rate for the trailing 3 months (0–1).</summary>
    public double Trailing3MonthsRate { get; init; }

    /// <summary>Utilization rate for the trailing 12 months (0–1).</summary>
    public double Trailing12MonthsRate { get; init; }

    /// <summary>Simple trend direction based on comparing current month to trailing 3-month average.</summary>
    public UtilizationTrend Trend { get; init; }

    /// <summary>Color-coded status based on the trailing 3-month utilization rate.</summary>
    public UtilizationStatus Status { get; init; }
}

public enum UtilizationTrend { Up, Flat, Down }

public enum UtilizationStatus
{
    /// <summary>Red: utilization &lt; 40% — idle alert.</summary>
    Idle,

    /// <summary>Yellow: utilization 40–70% — monitor.</summary>
    Monitor,

    /// <summary>Green: utilization &gt; 70% — healthy.</summary>
    Healthy
}

/// <summary>
/// Service for computing asset utilization metrics from checkout and maintenance records.
///
/// Methodology (AC-8 / tooltip):
///   Active hours   = sum of (ReturnedAtUtc – CheckedOutAtUtc) for completed checkouts
///                    + (UtcNow – CheckedOutAtUtc) for any currently open checkout
///                    within the measurement window.
///   Available hours = calendar hours in window – scheduled maintenance hours in window.
///   Utilization %   = Active hours / Available hours  (clamped 0–1).
/// </summary>
public interface IUtilizationService
{
    /// <summary>
    /// Computes utilization metrics for all owned assets, sorted by trailing-3-month rate ascending.
    /// </summary>
    IReadOnlyList<AssetUtilizationMetrics> GetFleetUtilization(DateTime asOf);

    /// <summary>
    /// Computes utilization metrics for a single asset.
    /// Returns null if the asset does not exist.
    /// </summary>
    AssetUtilizationMetrics? GetAssetUtilization(int assetId, DateTime asOf);

    /// <summary>
    /// Registers a maintenance downtime window for an asset.
    /// </summary>
    void AddMaintenanceDowntime(int assetId, DateTime start, DateTime end, string? reason = null);

    /// <summary>
    /// Returns all recorded maintenance downtime records for an asset.
    /// </summary>
    IReadOnlyList<MaintenanceDowntimeRecord> GetMaintenanceDowntime(int assetId);
}
