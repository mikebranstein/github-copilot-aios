using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Phase 1 rule-based maintenance scheduling service.
/// All usage data is derived exclusively from checkout/return records — no IoT/telematics.
/// Phase 2 ML capabilities are gated on 40% log completion rate and must NOT be introduced here.
/// AWS Lookout is prohibited per design constraints.
/// </summary>
public class MaintenanceService : IMaintenanceService
{
    private readonly IEquipmentService _equipmentService;

    private readonly List<ServiceInterval> _intervals = new();
    private readonly List<MaintenanceEvent> _events = new();
    private readonly List<AlertConfig> _alertConfigs = new();
    private readonly Dictionary<int, DateTime> _lastAlertSentAtUtc = new();

    private int _nextIntervalId = 1;
    private int _nextEventId = 1;
    private int _nextAlertConfigId = 1;

    // Default Caution threshold: within 10% of hours interval
    private const double CautionThresholdPercent = 0.10;

    public MaintenanceService(IEquipmentService equipmentService)
    {
        _equipmentService = equipmentService;
    }

    // ── Operating Hours ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public double GetOperatingHours(int assetId)
    {
        var records = _equipmentService.GetCheckoutHistory(assetId);
        double totalHours = 0;

        foreach (var record in records)
        {
            if (record.IsVoided) continue;

            if (record.ReturnedAtUtc.HasValue)
            {
                totalHours += (record.ReturnedAtUtc.Value - record.CheckedOutAtUtc).TotalHours;
            }
            // Open checkouts: per design, exclude open checkouts and flag if > 24 hours.
            // The hours calculation only uses completed (returned) records.
        }

        return Math.Round(totalHours, 2);
    }

    /// <inheritdoc />
    public double? GetAverageDailyHours(int assetId)
    {
        var cutoff = DateTime.UtcNow.AddDays(-90);
        var records = _equipmentService.GetCheckoutHistory(assetId)
            .Where(r => !r.IsVoided && r.ReturnedAtUtc.HasValue && r.CheckedOutAtUtc >= cutoff)
            .ToList();

        if (!records.Any()) return null;

        double totalHours = records
            .Sum(r => (r.ReturnedAtUtc!.Value - r.CheckedOutAtUtc).TotalHours);

        return Math.Round(totalHours / 90.0, 4);
    }

    // ── Service Status ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public MaintenanceBand GetServiceStatus(int assetId)
    {
        var item = _equipmentService.GetItem(assetId);
        if (item is null) return MaintenanceBand.NoData;

        var interval = GetServiceInterval(item.Category);
        if (interval is null) return MaintenanceBand.NoData;

        if (interval.IntervalType == IntervalType.Hours)
        {
            return ComputeHoursBand(assetId, interval);
        }
        else
        {
            return ComputeTimeBand(assetId, interval);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AssetServiceStatus> GetAllServiceStatuses()
    {
        var items = _equipmentService.GetAllItems();
        var statuses = new List<AssetServiceStatus>();

        foreach (var item in items)
        {
            var interval = GetServiceInterval(item.Category);
            var hasOpenCheckout = _equipmentService.GetActiveCheckoutRecord(item.Id) is not null;
            var alertConfig = GetOrCreateAlertConfig(item.Id);

            if (interval is null)
            {
                statuses.Add(new AssetServiceStatus
                {
                    AssetId = item.Id,
                    AssetName = item.Name,
                    Category = item.Category,
                    Band = MaintenanceBand.NoData,
                    HasIntervalConfigured = false,
                    HasOpenCheckout = hasOpenCheckout,
                    IsSnoozed = alertConfig.IsSnoozed
                });
                continue;
            }

            var status = BuildAssetServiceStatus(item, interval, alertConfig, hasOpenCheckout);
            statuses.Add(status);
        }

        return statuses.AsReadOnly();
    }

    /// <inheritdoc />
    public double? GetHoursToNextService(int assetId)
    {
        var item = _equipmentService.GetItem(assetId);
        if (item is null) return null;

        var interval = GetServiceInterval(item.Category);
        if (interval is null || interval.IntervalType != IntervalType.Hours) return null;

        var hoursUsed = GetHoursSinceLastService(assetId);
        return Math.Max(0, interval.IntervalValue - hoursUsed);
    }

    /// <inheritdoc />
    public DateTime? GetProjectedServiceDate(int assetId)
    {
        var item = _equipmentService.GetItem(assetId);
        if (item is null) return null;

        var interval = GetServiceInterval(item.Category);
        if (interval is null || interval.IntervalType != IntervalType.Hours) return null;

        var avgDaily = GetAverageDailyHours(assetId);
        if (!avgDaily.HasValue || avgDaily.Value <= 0) return null;

        var hoursRemaining = GetHoursToNextService(assetId);
        if (!hoursRemaining.HasValue || hoursRemaining.Value <= 0) return DateTime.UtcNow; // already overdue

        double daysToService = hoursRemaining.Value / avgDaily.Value;
        return DateTime.UtcNow.AddDays(daysToService);
    }

    // ── Service Intervals ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public ServiceInterval? GetServiceInterval(string category)
        => _intervals.FirstOrDefault(i => string.Equals(i.Category, category, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public IReadOnlyList<ServiceInterval> GetAllServiceIntervals()
        => _intervals.AsReadOnly();

    /// <inheritdoc />
    public ServiceInterval UpsertServiceInterval(string category, IntervalType intervalType, double intervalValue, int leadTimeDays = 14)
    {
        var existing = GetServiceInterval(category);
        if (existing is not null)
        {
            existing.IntervalType = intervalType;
            existing.IntervalValue = intervalValue;
            existing.LeadTimeDays = leadTimeDays;
            return existing;
        }

        var interval = new ServiceInterval
        {
            Id = _nextIntervalId++,
            Category = category,
            IntervalType = intervalType,
            IntervalValue = intervalValue,
            LeadTimeDays = leadTimeDays,
            CreatedAtUtc = DateTime.UtcNow
        };
        _intervals.Add(interval);
        return interval;
    }

    // ── Maintenance Events ────────────────────────────────────────────────────

    /// <inheritdoc />
    public MaintenanceEvent LogMaintenanceEvent(int assetId, string eventType, DateTime eventDate, double hoursAtService, string? technicianName = null, string? notes = null)
    {
        var evt = new MaintenanceEvent
        {
            Id = _nextEventId++,
            AssetId = assetId,
            EventType = eventType,
            EventDate = eventDate,
            HoursAtService = hoursAtService,
            TechnicianName = technicianName,
            Notes = notes,
            CreatedAtUtc = DateTime.UtcNow
        };
        _events.Add(evt);
        return evt;
    }

    /// <inheritdoc />
    public IReadOnlyList<MaintenanceEvent> GetMaintenanceHistory(int assetId)
        => _events
            .Where(e => e.AssetId == assetId)
            .OrderByDescending(e => e.EventDate)
            .ToList()
            .AsReadOnly();

    /// <inheritdoc />
    public MaintenanceEvent? GetLastMaintenanceEvent(int assetId)
        => _events
            .Where(e => e.AssetId == assetId)
            .OrderByDescending(e => e.EventDate)
            .FirstOrDefault();

    // ── Alert Configuration ────────────────────────────────────────────────────

    /// <inheritdoc />
    public AlertConfig GetOrCreateAlertConfig(int assetId)
    {
        var config = _alertConfigs.FirstOrDefault(c => c.AssetId == assetId);
        if (config is not null) return config;

        config = new AlertConfig
        {
            Id = _nextAlertConfigId++,
            AssetId = assetId,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _alertConfigs.Add(config);
        return config;
    }

    /// <inheritdoc />
    public void SnoozeAlert(int assetId, int days)
    {
        var config = GetOrCreateAlertConfig(assetId);
        config.SnoozedUntilUtc = DateTime.UtcNow.AddDays(days);
        config.UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public void UpdateNotificationRecipients(int assetId, string recipients)
    {
        var config = GetOrCreateAlertConfig(assetId);
        config.NotificationRecipients = recipients;
        config.UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public IReadOnlyList<AssetServiceStatus> GetPendingAlerts()
    {
        var threshold = DateTime.UtcNow.AddMinutes(-15);
        var allStatuses = GetAllServiceStatuses();

        return allStatuses
            .Where(s =>
                (s.Band == MaintenanceBand.Caution || s.Band == MaintenanceBand.Overdue) &&
                !s.IsSnoozed &&
                (!_lastAlertSentAtUtc.TryGetValue(s.AssetId, out var lastSent) || lastSent <= threshold))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>Records that an alert was sent for the given asset (used by notification job).</summary>
    public void RecordAlertSent(int assetId)
    {
        _lastAlertSentAtUtc[assetId] = DateTime.UtcNow;
    }

    // ── Phase 1 Gate Metric ────────────────────────────────────────────────────

    /// <inheritdoc />
    public double GetMaintenanceLogCompletionRate()
    {
        var allItems = _equipmentService.GetAllItems();
        if (!allItems.Any()) return 0.0;

        // Only count items that have a service interval configured (i.e., enrolled assets)
        var enrolledItems = allItems
            .Where(i => GetServiceInterval(i.Category) is not null)
            .ToList();

        if (!enrolledItems.Any()) return 0.0;

        var cutoff = DateTime.UtcNow.AddDays(-90);
        int withEvents = enrolledItems.Count(item =>
            _events.Any(e => e.AssetId == item.Id && e.EventDate >= cutoff));

        return Math.Round((double)withEvents / enrolledItems.Count * 100, 1);
    }

    // ── Downtime Cost Calculator ────────────────────────────────────────────────

    /// <inheritdoc />
    public DowntimeCostSummary CalculateDowntimeCost(int assetCount, double dailyCostEstimate, double avgRepairDays, double estimatedIncidentsPerYear)
    {
        double annualCost = estimatedIncidentsPerYear * avgRepairDays * dailyCostEstimate;
        double annualSavings = annualCost * 0.80;

        return new DowntimeCostSummary
        {
            AssetCount = assetCount,
            DailyCostEstimate = dailyCostEstimate,
            AvgRepairDays = avgRepairDays,
            EstimatedIncidentsPerYear = estimatedIncidentsPerYear,
            EstimatedAnnualDowntimeCost = Math.Round(annualCost, 2),
            EstimatedAnnualSavings80Percent = Math.Round(annualSavings, 2)
        };
    }

    // ── Private Helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns hours used since the last maintenance event (or all time if no event logged).
    /// This is the value compared against the interval.
    /// </summary>
    private double GetHoursSinceLastService(int assetId)
    {
        var lastEvent = GetLastMaintenanceEvent(assetId);
        if (lastEvent is null)
        {
            // No service event: use total operating hours
            return GetOperatingHours(assetId);
        }

        // Hours accumulated since last service date
        var records = _equipmentService.GetCheckoutHistory(assetId);
        double hours = 0;
        foreach (var record in records)
        {
            if (record.IsVoided) continue;
            if (!record.ReturnedAtUtc.HasValue) continue;
            if (record.CheckedOutAtUtc < lastEvent.EventDate) continue;

            hours += (record.ReturnedAtUtc.Value - record.CheckedOutAtUtc).TotalHours;
        }
        return Math.Round(hours, 2);
    }

    private MaintenanceBand ComputeHoursBand(int assetId, ServiceInterval interval)
    {
        var records = _equipmentService.GetCheckoutHistory(assetId)
            .Where(r => !r.IsVoided && r.ReturnedAtUtc.HasValue)
            .ToList();

        if (!records.Any())
            return MaintenanceBand.NoData;

        double hoursUsed = GetHoursSinceLastService(assetId);
        double cautionThreshold = interval.IntervalValue * (1.0 - CautionThresholdPercent);

        if (hoursUsed >= interval.IntervalValue)
            return MaintenanceBand.Overdue;

        if (hoursUsed >= cautionThreshold)
            return MaintenanceBand.Caution;

        return MaintenanceBand.InRange;
    }

    private MaintenanceBand ComputeTimeBand(int assetId, ServiceInterval interval)
    {
        var lastEvent = GetLastMaintenanceEvent(assetId);
        DateTime baseDate = lastEvent?.EventDate ?? DateTime.MinValue;

        if (baseDate == DateTime.MinValue)
        {
            // No maintenance event: check if asset has any checkout history as proxy for "enrolled"
            var records = _equipmentService.GetCheckoutHistory(assetId)
                .Where(r => !r.IsVoided)
                .ToList();
            if (!records.Any()) return MaintenanceBand.NoData;

            // Use asset creation proxy: treat as if service was at epoch (worst case — overdue)
            return MaintenanceBand.Overdue;
        }

        var nextDueDate = baseDate.AddDays(interval.IntervalValue);
        var daysRemaining = (nextDueDate - DateTime.UtcNow).TotalDays;

        if (daysRemaining < 0)
            return MaintenanceBand.Overdue;

        if (daysRemaining <= interval.LeadTimeDays)
            return MaintenanceBand.Caution;

        return MaintenanceBand.InRange;
    }

    private AssetServiceStatus BuildAssetServiceStatus(EquipmentItem item, ServiceInterval interval, AlertConfig alertConfig, bool hasOpenCheckout)
    {
        var status = new AssetServiceStatus
        {
            AssetId = item.Id,
            AssetName = item.Name,
            Category = item.Category,
            HasIntervalConfigured = true,
            HasOpenCheckout = hasOpenCheckout,
            IsSnoozed = alertConfig.IsSnoozed
        };

        if (interval.IntervalType == IntervalType.Hours)
        {
            status.OperatingHours = GetOperatingHours(item.Id);
            status.IntervalHours = interval.IntervalValue;
            status.HoursRemaining = GetHoursToNextService(item.Id);
            status.ProjectedServiceDate = GetProjectedServiceDate(item.Id);
            status.Band = ComputeHoursBand(item.Id, interval);
        }
        else
        {
            var lastEvent = GetLastMaintenanceEvent(item.Id);
            DateTime baseDate = lastEvent?.EventDate ?? DateTime.MinValue;

            if (baseDate != DateTime.MinValue)
            {
                var nextDue = baseDate.AddDays(interval.IntervalValue);
                status.NextDueDate = nextDue;
                status.DaysRemaining = (int)Math.Round((nextDue - DateTime.UtcNow).TotalDays);
            }
            status.Band = ComputeTimeBand(item.Id, interval);
        }

        return status;
    }
}
