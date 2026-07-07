using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EquipmentTracker.Web.Controllers;

public class EquipmentController : Controller
{
    private readonly IEquipmentService _equipmentService;
    private readonly ISiteService _siteService;
    private readonly IConfiguration _configuration;

    public EquipmentController(IEquipmentService equipmentService, ISiteService siteService, IConfiguration configuration)
    {
        _equipmentService = equipmentService;
        _siteService = siteService;
        _configuration = configuration;
    }

    public IActionResult Index()
    {
        int overdueThresholdDays = _configuration.GetValue<int>("Checkout:OverdueThresholdDays", 7);
        var utcNow = DateTime.UtcNow;
        var items = _equipmentService.GetAllItems();
        var siteNames = _siteService.GetAllSites().ToDictionary(site => site.Id, site => site.Name);

        var rows = items.Select(item =>
        {
            var activeRecord = item.IsAvailable
                ? null
                : _equipmentService.GetActiveCheckoutRecord(item.Id);

            int daysCheckedOut = 0;
            bool isOverdue = false;

            if (activeRecord is not null)
            {
                daysCheckedOut = (int)(utcNow - activeRecord.CheckedOutAtUtc).TotalDays;
                isOverdue = daysCheckedOut >= overdueThresholdDays;
            }

            return new EquipmentListItemViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Category = item.Category,
                IsAvailable = item.IsAvailable,
                IsOverdue = isOverdue,
                BorrowerName = activeRecord?.BorrowerName,
                DaysCheckedOut = daysCheckedOut,
                Status = item.Status,
                SiteName = item.SiteId.HasValue && siteNames.TryGetValue(item.SiteId.Value, out var siteName) ? siteName : null
            };
        }).ToList();

        var model = new EquipmentListViewModel
        {
            Items = rows,
            AvailableCount = rows.Count(r => r.Status == EquipmentTracker.Web.Models.EquipmentStatus.Available)
        };
        return View(model);
    }

    public IActionResult Create()
    {
        return View(new CreateEquipmentViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(CreateEquipmentViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        _equipmentService.CreateItem(model.Name, model.Category);

        TempData["SuccessMessage"] = $"'{model.Name}' was added successfully.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Checkout(int id)
    {
        var item = _equipmentService.GetItem(id);
        if (item is null)
            return NotFound();

        if (!item.IsAvailable)
        {
            TempData["ErrorMessage"] = $"'{item.Name}' is not available for checkout.";
            return RedirectToAction(nameof(Index));
        }

        var model = new CheckoutViewModel
        {
            EquipmentItemId = item.Id,
            EquipmentItemName = item.Name,
            ConfirmedSiteId = item.SiteId
        };
        PopulateCheckoutSiteFields(model, item);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Checkout(CheckoutViewModel model)
    {
        var item = _equipmentService.GetItem(model.EquipmentItemId);
        if (item is null)
            return NotFound();

        if (model.ConfirmedSiteId.HasValue)
        {
            var selectedSite = _siteService.GetSite(model.ConfirmedSiteId.Value);
            if (selectedSite is null || !selectedSite.IsActive)
                ModelState.AddModelError(nameof(model.ConfirmedSiteId), "Please choose an active site.");
        }

        if (!ModelState.IsValid)
        {
            PopulateCheckoutSiteFields(model, item);
            return View(model);
        }

        var success = _equipmentService.Checkout(model.EquipmentItemId, model.BorrowerName, newSiteId: model.ConfirmedSiteId);
        if (!success)
        {
            var currentHolder = _equipmentService.GetCurrentHolder(model.EquipmentItemId);
            var errorMessage = currentHolder is not null
                ? $"'{model.EquipmentItemName}' is already checked out by {currentHolder}."
                : $"'{model.EquipmentItemName}' is no longer available for checkout.";
            ModelState.AddModelError(string.Empty, errorMessage);
            PopulateCheckoutSiteFields(model, item);
            return View(model);
        }

        TempData["SuccessMessage"] = $"'{model.EquipmentItemName}' checked out to {model.BorrowerName}.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult History(int id)
    {
        var item = _equipmentService.GetItem(id);
        if (item is null)
            return NotFound();

        var history = _equipmentService.GetCheckoutHistory(id);
        var model = new ItemCheckoutHistoryViewModel
        {
            EquipmentItemId = item.Id,
            EquipmentItemName = item.Name,
            History = history
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Return(int id)
    {
        var item = _equipmentService.GetItem(id);
        if (item is null)
            return NotFound();

        var success = _equipmentService.Return(id);
        if (!success)
        {
            TempData["ErrorMessage"] = $"'{item.Name}' is already available and cannot be returned again.";
        }
        else
        {
            TempData["SuccessMessage"] = $"'{item.Name}' has been returned successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    private void PopulateCheckoutSiteFields(CheckoutViewModel model, EquipmentTracker.Web.Models.EquipmentItem item)
    {
        model.CurrentSiteName = item.SiteId.HasValue ? _siteService.GetSite(item.SiteId.Value)?.Name : null;
        model.SiteOptions =
        [
            new SelectListItem("No change", ""),
            .. _siteService.GetActiveSites().Select(site => new SelectListItem(site.Name, site.Id.ToString()))
        ];
    }
}
