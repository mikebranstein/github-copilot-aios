using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Manages manually-entered rental cost data for asset categories (MVP).
/// </summary>
public interface IRentalCostService
{
    /// <summary>Adds a manual rental cost entry for an asset category.</summary>
    RentalCostEntry AddEntry(string assetCategory, DateTime periodStart, DateTime periodEnd,
        decimal costAmount, string enteredBy, string currency = "USD");

    /// <summary>Returns all rental cost entries for a category.</summary>
    IReadOnlyList<RentalCostEntry> GetEntriesForCategory(string assetCategory);

    /// <summary>Returns the YTD rental cost sum for an asset category (Jan 1 of asOf.Year → asOf).</summary>
    decimal GetYtdCost(string assetCategory, DateTime asOf);

    /// <summary>
    /// Returns the number of days of rental data available for a category
    /// (span from earliest PeriodStart to asOf, only if entries exist).
    /// </summary>
    int GetDataDaysAvailable(string assetCategory, DateTime asOf);
}
