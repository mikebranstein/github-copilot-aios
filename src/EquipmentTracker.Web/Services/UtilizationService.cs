using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-memory implementation of IUtilizationService.
/// Computes utilization from checkout records and maintenance downtime windows.
///
/// Utilization Methodology (documented per AC-8):
///   Active hours   = sum of checkout durations within the measurement window
///                    (open checkouts contribute up to asOf).
///   Available hours = total calendar hours in window
///                     minus scheduled maintenance hours overlapping the window.
///   Utilization %   = Active hours ÷ Available hours  (clamped 0.0 – 1.0).
/// </summary>
public class UtilizationService : IUtilizationService
{
    private readonly IEquipmentService _equipmentService;
    private readonly List<MaintenanceDowntimeRecord> _maintenanceRecords = new();
    private int _nextMaintenanceId = 1;

    // Trend threshold: change of ≥5 percentage points is treated as Up/Down; otherwise Flat.
    private const double TrendThreshold = 0.05;

    public UtilizationService(IEquipmentService equipmentService)
    {
        _equipmentService = equipmentService;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public IReadOnlyList<AssetUtilizationMetrics> GetFleetUtilization(DateTime asOf)
    {
        return _equipmentService.GetAllItems()
            .Select(item => ComputeMetrics(item, asOf))
            .OrderBy(m => m.Trailing3MonthsRate)
            .ToList()
            .AsReadOnly();
    }

    public AssetUtilizationMetrics? GetAssetUtilization(int assetId, DateTime asOf)
    {
        var item = _equipmentService.GetItem(assetId);
        return item is null ? null : ComputeMetrics(item, asOf);
    }

    public void AddMaintenanceDowntime(int assetId, DateTime start, DateTime end, string? reason = null)
    {
        if (end <= start) throw new ArgumentException("Maintenance end must be after start.");
        _maintenanceRecords.Add(new MaintenanceDowntimeRecord
        {
            Id = _nextMaintenanceId++,
            EquipmentItemId = assetId,
            DowntimeStart = start,
            DowntimeEnd = end,
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        });
    }

    public IReadOnlyList<MaintenanceDowntimeRecord> GetMaintenanceDowntime(int assetId) =>
        _maintenanceRecords.Where(r => r.EquipmentItemId == assetId).ToList().AsReadOnly();

    // ── Core computation ──────────────────────────────────────────────────────

    private AssetUtilizationMetrics ComputeMetrics(EquipmentItem item, DateTime asOf)
    {
        // Define period boundaries (all UTC)
        var currentMonthStart = new DateTime(asOf.Year, asOf.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var trailing3Start = asOf.AddMonths(-3);
        var trailing12Start = asOf.AddMonths(-12);

        double currentMonth = ComputeRate(item.Id, currentMonthStart, asOf);
        double trailing3 = ComputeRate(item.Id, trailing3Start, asOf);
        double trailing12 = ComputeRate(item.Id, trailing12Start, asOf);

        var trend = currentMonth - trailing3 >= TrendThreshold ? UtilizationTrend.Up
                  : trailing3 - currentMonth >= TrendThreshold ? UtilizationTrend.Down
                  : UtilizationTrend.Flat;

        var status = trailing3 < 0.40 ? UtilizationStatus.Idle
                   : trailing3 <= 0.70 ? UtilizationStatus.Monitor
                   : UtilizationStatus.Healthy;

        return new AssetUtilizationMetrics
        {
            AssetId = item.Id,
            AssetName = item.Name,
            AssetCategory = item.Category,
            CurrentMonthRate = currentMonth,
            Trailing3MonthsRate = trailing3,
            Trailing12MonthsRate = trailing12,
            Trend = trend,
            Status = status
        };
    }

    /// <summary>
    /// Computes utilization rate for one asset over [windowStart, windowEnd].
    /// Returns 0.0 if available hours is zero (no calendar time yet).
    /// </summary>
    internal double ComputeRate(int assetId, DateTime windowStart, DateTime windowEnd)
    {
        if (windowEnd <= windowStart) return 0.0;

        double calendarHours = (windowEnd - windowStart).TotalHours;
        double maintenanceHours = GetMaintenanceHours(assetId, windowStart, windowEnd);
        double availableHours = Math.Max(0, calendarHours - maintenanceHours);

        if (availableHours <= 0) return 0.0;

        double activeHours = GetActiveHours(assetId, windowStart, windowEnd);
        return Math.Min(1.0, activeHours / availableHours);
    }

    private double GetActiveHours(int assetId, DateTime windowStart, DateTime windowEnd)
    {
        var records = _equipmentService.GetCheckoutHistory(assetId);
        double total = 0.0;

        foreach (var record in records)
        {
            var start = record.CheckedOutAtUtc;
            var end = record.ReturnedAtUtc ?? windowEnd;  // open checkout counts up to asOf

            // Clip to measurement window
            var clippedStart = start < windowStart ? windowStart : start;
            var clippedEnd = end > windowEnd ? windowEnd : end;

            if (clippedEnd > clippedStart)
                total += (clippedEnd - clippedStart).TotalHours;
        }

        return total;
    }

    private double GetMaintenanceHours(int assetId, DateTime windowStart, DateTime windowEnd)
    {
        return _maintenanceRecords
            .Where(r => r.EquipmentItemId == assetId)
            .Sum(r =>
            {
                var clippedStart = r.DowntimeStart < windowStart ? windowStart : r.DowntimeStart;
                var clippedEnd = r.DowntimeEnd > windowEnd ? windowEnd : r.DowntimeEnd;
                return clippedEnd > clippedStart ? (clippedEnd - clippedStart).TotalHours : 0.0;
            });
    }
}
