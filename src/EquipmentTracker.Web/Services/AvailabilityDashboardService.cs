using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.ViewModels;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Reads from existing equipment/checkout/condition data to compute compound availability.
/// This service is read-only with respect to the existing data model.
/// Added for Issue #118 — Real-Time Equipment Availability Dashboard.
/// </summary>
public class AvailabilityDashboardService : IAvailabilityDashboardService
{
    private readonly IEquipmentService _equipmentService;
    private readonly ISoftHoldService _softHoldService;

    public AvailabilityDashboardService(
        IEquipmentService equipmentService,
        ISoftHoldService softHoldService)
    {
        _equipmentService = equipmentService;
        _softHoldService = softHoldService;
    }

    public EquipmentAvailabilityItem? GetItemAvailability(int equipmentItemId)
    {
        var item = _equipmentService.GetItem(equipmentItemId);
        if (item is null) return null;

        return ComputeAvailability(item);
    }

    public AvailabilityDashboardViewModel GetDashboard(string? siteFilter = null, string? categoryFilter = null)
    {
        var allItems = _equipmentService.GetAllItems();
        var allSites = allItems
            .Where(i => !string.IsNullOrWhiteSpace(i.SiteName))
            .Select(i => i.SiteName!)
            .Distinct()
            .OrderBy(s => s)
            .ToList();
        var allCategories = allItems
            .Select(i => i.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        // Filter
        var filtered = allItems.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(siteFilter))
            filtered = filtered.Where(i => i.SiteName == siteFilter);
        if (!string.IsNullOrWhiteSpace(categoryFilter) &&
            !string.Equals(categoryFilter, "All", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(i => i.Category == categoryFilter);

        var availabilityItems = filtered
            .Select(ComputeAvailability)
            .ToList();

        var categories = availabilityItems
            .GroupBy(i => i.Category)
            .Select(g => new CategoryAvailabilitySummary
            {
                Category = g.Key,
                SiteName = siteFilter,
                AvailableCount = g.Count(i => i.CompoundStatus == EquipmentCompoundStatus.Available),
                TotalCount = g.Count(),
                Items = g.OrderBy(i => i.Name).ToList().AsReadOnly()
            })
            .OrderBy(c => c.Category)
            .ToList();

        return new AvailabilityDashboardViewModel
        {
            Categories = categories.AsReadOnly(),
            DataFreshnessUtc = DateTime.UtcNow,
            SiteFilter = siteFilter,
            CategoryFilter = categoryFilter,
            AvailableSites = allSites.AsReadOnly(),
            AvailableCategories = allCategories.AsReadOnly()
        };
    }

    public IReadOnlyList<string> GetAllSites() =>
        _equipmentService.GetAllItems()
            .Where(i => !string.IsNullOrWhiteSpace(i.SiteName))
            .Select(i => i.SiteName!)
            .Distinct()
            .OrderBy(s => s)
            .ToList()
            .AsReadOnly();

    public IReadOnlyList<string> GetAllCategories() =>
        _equipmentService.GetAllItems()
            .Select(i => i.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList()
            .AsReadOnly();

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private EquipmentAvailabilityItem ComputeAvailability(EquipmentItem item)
    {
        var result = new EquipmentAvailabilityItem
        {
            ItemId = item.Id,
            Name = item.Name,
            Category = item.Category,
            SiteName = item.SiteName
        };

        // Condition (b): Serviceable — Maintenance or Lost blocks availability
        if (item.LifecycleStatus == EquipmentLifecycleStatus.Maintenance ||
            item.LifecycleStatus == EquipmentLifecycleStatus.Lost)
        {
            result.CompoundStatus = EquipmentCompoundStatus.UnderMaintenance;
            result.BlockingReason = item.LifecycleStatus == EquipmentLifecycleStatus.Lost
                ? "Lost / Decommissioned"
                : "Under Maintenance";
            return result;
        }

        // Condition (c): Known location
        if (string.IsNullOrWhiteSpace(item.SiteName))
        {
            result.CompoundStatus = EquipmentCompoundStatus.LocationUnknown;
            result.BlockingReason = "Location Unknown";
            return result;
        }

        // Condition (a): Not checked out
        if (!item.IsAvailable)
        {
            result.CompoundStatus = EquipmentCompoundStatus.CheckedOut;
            result.BlockingReason = "Checked Out";
            return result;
        }

        // Condition (d): No active soft hold
        var hold = _softHoldService.GetActiveHold(item.Id);
        if (hold is not null)
        {
            result.CompoundStatus = EquipmentCompoundStatus.SoftHeld;
            result.ActiveSoftHoldId = hold.Id;
            result.SoftHoldOwnerUserId = hold.UserId;
            result.SoftHoldRemainingMinutes = hold.RemainingMinutes();
            result.BlockingReason = $"Reserved — expires in {hold.RemainingMinutes()} min";
            return result;
        }

        // All four conditions met
        result.CompoundStatus = EquipmentCompoundStatus.Available;
        return result;
    }
}
