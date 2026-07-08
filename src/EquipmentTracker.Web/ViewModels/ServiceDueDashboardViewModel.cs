using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>Fleet-wide Service Due Dashboard view model (AC-2).</summary>
public class ServiceDueDashboardViewModel
{
    public IReadOnlyList<AssetServiceStatus> Statuses { get; set; } = Array.Empty<AssetServiceStatus>();
    public string? FilterBand { get; set; }
    public string? FilterCategory { get; set; }
    public int? FilterSiteId { get; set; }
    public string? SortBy { get; set; }

    public IEnumerable<AssetServiceStatus> FilteredStatuses
    {
        get
        {
            var result = Statuses.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(FilterBand) &&
                Enum.TryParse<MaintenanceBand>(FilterBand, true, out var band))
                result = result.Where(s => s.Band == band);

            if (!string.IsNullOrWhiteSpace(FilterCategory))
                result = result.Where(s => string.Equals(s.Category, FilterCategory, StringComparison.OrdinalIgnoreCase));

            if (FilterSiteId.HasValue)
                result = result.Where(s => s.SiteId == FilterSiteId.Value);

            return SortBy switch
            {
                "band" => result.OrderBy(s => s.Band),
                "hours" => result.OrderBy(s => s.HoursRemaining ?? double.MaxValue),
                "name" => result.OrderBy(s => s.AssetName),
                _ => result.OrderBy(s => s.Band) // default: most urgent first
            };
        }
    }

    public int OverdueCount => Statuses.Count(s => s.Band == MaintenanceBand.Overdue);
    public int CautionCount => Statuses.Count(s => s.Band == MaintenanceBand.Caution);
    public int InRangeCount => Statuses.Count(s => s.Band == MaintenanceBand.InRange);
    public int NoDataCount => Statuses.Count(s => s.Band == MaintenanceBand.NoData);
}
