using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.ViewModels;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Computes compound equipment availability and provides the filtered dashboard view.
/// Added for Issue #118 — Real-Time Equipment Availability Dashboard.
/// </summary>
public interface IAvailabilityDashboardService
{
    /// <summary>
    /// Returns the compound availability status for one equipment item.
    /// An item is Available only when ALL four conditions are simultaneously true:
    ///   (a) not checked out, (b) serviceable, (c) location known, (d) no active soft hold.
    /// </summary>
    EquipmentAvailabilityItem? GetItemAvailability(int equipmentItemId);

    /// <summary>
    /// Returns the full availability dashboard view, optionally filtered by site and/or category.
    /// Includes category-level summaries and item-level drill-down.
    /// </summary>
    AvailabilityDashboardViewModel GetDashboard(string? siteFilter = null, string? categoryFilter = null);

    /// <summary>Returns all distinct site names in the system (for the filter dropdown).</summary>
    IReadOnlyList<string> GetAllSites();

    /// <summary>Returns all distinct category names in the system (for the filter dropdown).</summary>
    IReadOnlyList<string> GetAllCategories();
}
