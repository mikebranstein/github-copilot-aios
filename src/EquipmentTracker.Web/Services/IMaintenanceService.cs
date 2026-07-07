using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Smart Maintenance Scheduling service — Phase 1 (rule-based only, no ML).
/// Derives operating hours from checkout/return records; supports hours-based and time-based intervals.
/// </summary>
public interface IMaintenanceService
{
    // ── Operating Hours ───────────────────────────────────────────────────────

    /// <summary>
    /// Calculates total operating hours for an asset from completed checkout/return records.
    /// Open checkouts older than 24 hours are flagged; checkouts with no return are excluded.
    /// </summary>
    double GetOperatingHours(int assetId);

    /// <summary>Returns the average daily operating hours over the last 90 days, or null if insufficient data.</summary>
    double? GetAverageDailyHours(int assetId);

    // ── Service Status ────────────────────────────────────────────────────────

    /// <summary>Returns the current maintenance band for a single asset.</summary>
    MaintenanceBand GetServiceStatus(int assetId);

    /// <summary>Returns fleet-wide service status summary for the dashboard.</summary>
    IReadOnlyList<AssetServiceStatus> GetAllServiceStatuses();

    /// <summary>
    /// Returns hours remaining until next service for a given asset (hours-based intervals only).
    /// Null if no interval configured or interval type is TimeBased.
    /// </summary>
    double? GetHoursToNextService(int assetId);

    /// <summary>
    /// Returns projected service due date based on average daily usage rate.
    /// Null if average daily hours is zero or insufficient data.
    /// </summary>
    DateTime? GetProjectedServiceDate(int assetId);

    // ── Service Intervals ─────────────────────────────────────────────────────

    /// <summary>Gets the service interval configured for a category, or null if none configured.</summary>
    ServiceInterval? GetServiceInterval(string category);

    /// <summary>Returns all configured service intervals.</summary>
    IReadOnlyList<ServiceInterval> GetAllServiceIntervals();

    /// <summary>Creates or updates the service interval for an equipment category.</summary>
    ServiceInterval UpsertServiceInterval(string category, IntervalType intervalType, double intervalValue, int leadTimeDays = 14);

    // ── Maintenance Events ────────────────────────────────────────────────────

    /// <summary>
    /// Logs a maintenance event for an asset.
    /// Resets the service interval counter from the event date/hours.
    /// Returns the created event.
    /// </summary>
    MaintenanceEvent LogMaintenanceEvent(int assetId, string eventType, DateTime eventDate, double hoursAtService, string? technicianName = null, string? notes = null);

    /// <summary>Returns all maintenance events for an asset, newest first. 7-year retention enforced.</summary>
    IReadOnlyList<MaintenanceEvent> GetMaintenanceHistory(int assetId);

    /// <summary>Returns the most recent maintenance event for an asset, or null.</summary>
    MaintenanceEvent? GetLastMaintenanceEvent(int assetId);

    // ── Alert Configuration ────────────────────────────────────────────────────

    /// <summary>Gets or creates the alert config for an asset.</summary>
    AlertConfig GetOrCreateAlertConfig(int assetId);

    /// <summary>Snoozes alerts for an asset for the specified number of days (7, 14, or 30).</summary>
    void SnoozeAlert(int assetId, int days);

    /// <summary>Updates notification recipients for an asset.</summary>
    void UpdateNotificationRecipients(int assetId, string recipients);

    /// <summary>
    /// Returns assets in Caution or Overdue status that are not snoozed and have not been notified
    /// within the past 15 minutes. Used by the background notification job.
    /// </summary>
    IReadOnlyList<AssetServiceStatus> GetPendingAlerts();

    // ── Phase 1 Gate Metric ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the percentage of enrolled assets that have at least one logged maintenance
    /// event in the past 90 days. Phase 2 ML gate: must reach 40%.
    /// </summary>
    double GetMaintenanceLogCompletionRate();

    // ── Downtime Cost Calculator ────────────────────────────────────────────────

    /// <summary>
    /// Calculates the estimated annual downtime cost summary for the onboarding screen.
    /// </summary>
    DowntimeCostSummary CalculateDowntimeCost(int assetCount, double dailyCostEstimate, double avgRepairDays, double estimatedIncidentsPerYear);
}

/// <summary>Fleet-wide service status for a single asset on the dashboard.</summary>
public class AssetServiceStatus
{
    public int AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int? SiteId { get; set; }
    public MaintenanceBand Band { get; set; }
    public double OperatingHours { get; set; }
    public double? IntervalHours { get; set; }
    public double? HoursRemaining { get; set; }
    public DateTime? ProjectedServiceDate { get; set; }

    /// <summary>For time-based intervals: the next due date.</summary>
    public DateTime? NextDueDate { get; set; }

    /// <summary>For time-based intervals: days remaining until due.</summary>
    public int? DaysRemaining { get; set; }

    public bool IsSnoozed { get; set; }
    public bool HasOpenCheckout { get; set; }
    public bool HasIntervalConfigured { get; set; }
}

/// <summary>Downtime cost calculation result for the onboarding screen (AC-5).</summary>
public class DowntimeCostSummary
{
    public int AssetCount { get; set; }
    public double DailyCostEstimate { get; set; }
    public double AvgRepairDays { get; set; }
    public double EstimatedIncidentsPerYear { get; set; }
    public double EstimatedAnnualDowntimeCost { get; set; }
    public double EstimatedAnnualSavings80Percent { get; set; }
}
