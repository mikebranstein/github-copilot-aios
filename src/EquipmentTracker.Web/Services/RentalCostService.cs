using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-memory implementation of IRentalCostService.
/// </summary>
public class RentalCostService : IRentalCostService
{
    private readonly List<RentalCostEntry> _entries = new();
    private int _nextId = 1;

    public RentalCostEntry AddEntry(string assetCategory, DateTime periodStart, DateTime periodEnd,
        decimal costAmount, string enteredBy, string currency = "USD")
    {
        var entry = new RentalCostEntry
        {
            Id = _nextId++,
            AssetCategory = assetCategory,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            CostAmount = costAmount,
            Currency = currency,
            EnteredBy = enteredBy,
            EntrySource = "MANUAL",
            CreatedAt = DateTime.UtcNow
        };
        _entries.Add(entry);
        return entry;
    }

    public IReadOnlyList<RentalCostEntry> GetEntriesForCategory(string assetCategory) =>
        _entries
            .Where(e => e.AssetCategory.Equals(assetCategory, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.PeriodStart)
            .ToList()
            .AsReadOnly();

    public decimal GetYtdCost(string assetCategory, DateTime asOf)
    {
        var ytdStart = new DateTime(asOf.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return _entries
            .Where(e => e.AssetCategory.Equals(assetCategory, StringComparison.OrdinalIgnoreCase)
                     && e.PeriodStart >= ytdStart
                     && e.PeriodStart <= asOf)
            .Sum(e => e.CostAmount);
    }

    public int GetDataDaysAvailable(string assetCategory, DateTime asOf)
    {
        var entries = _entries
            .Where(e => e.AssetCategory.Equals(assetCategory, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!entries.Any()) return 0;

        var earliest = entries.Min(e => e.PeriodStart);
        return Math.Max(0, (int)(asOf - earliest).TotalDays);
    }
}
