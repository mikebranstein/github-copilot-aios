using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ModelStatus = EquipmentTracker.Web.Models.EquipmentStatus;

namespace EquipmentTracker.Web.Controllers;

public class AvailabilityController : Controller
{
    private readonly IEquipmentService _equipmentService;
    private readonly ISiteService _siteService;

    public AvailabilityController(IEquipmentService equipmentService, ISiteService siteService)
    {
        _equipmentService = equipmentService;
        _siteService = siteService;
    }

    public IActionResult Index(int? siteId, string? status)
    {
        var allItems = _equipmentService.GetAllItems();
        var allSites = _siteService.GetAllSites();
        var siteById = allSites.ToDictionary(s => s.Id, s => s.Name);

        var filtered = siteId.HasValue
            ? allItems.Where(i => i.SiteId == siteId.Value).ToList()
            : allItems.ToList();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ModelStatus>(status, true, out var parsedStatus))
            filtered = filtered.Where(i => i.Status == parsedStatus).ToList();

        var items = filtered.Select(i => new AvailabilityItemViewModel
        {
            Id = i.Id,
            Name = i.Name,
            Category = i.Category,
            Status = i.Status,
            SiteName = i.SiteId.HasValue && siteById.TryGetValue(i.SiteId.Value, out var siteName) ? siteName : null,
            LastUpdatedAtUtc = i.LastUpdatedAtUtc
        }).ToList();

        var siteOptions = new List<SelectListItem>
        {
            new("All Sites", "")
        };
        siteOptions.AddRange(_siteService.GetActiveSites().Select(s => new SelectListItem(s.Name, s.Id.ToString())));

        var vm = new AvailabilityViewModel
        {
            Items = items,
            SelectedSiteId = siteId,
            SelectedStatus = status,
            SiteOptions = siteOptions
        };

        return View(vm);
    }
}
