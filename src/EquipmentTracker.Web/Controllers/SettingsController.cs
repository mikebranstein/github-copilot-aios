using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EquipmentTracker.Web.Controllers;

[Authorize]
public class SettingsController : Controller
{
    private readonly IUserService _userService;
    private readonly ISiteService _siteService;

    public SettingsController(IUserService userService, ISiteService siteService)
    {
        _userService = userService;
        _siteService = siteService;
    }

    [HttpGet]
    public IActionResult Notifications()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var user = _userService.GetById(userId);
        var vm = new NotificationSettingsViewModel
        {
            NotificationsEnabled = user?.NotificationsEnabled ?? false
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Notifications(NotificationSettingsViewModel model)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        _userService.SetNotificationsEnabled(userId, model.NotificationsEnabled);
        TempData["Success"] = "Notification settings saved.";
        return RedirectToAction(nameof(Notifications));
    }

    [HttpGet]
    public IActionResult SiteManagement()
    {
        if (!IsCoordinator())
            return Forbid();

        return View(BuildSiteManagementViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateSite(CreateSiteViewModel model)
    {
        if (!IsCoordinator())
            return Forbid();

        if (!ModelState.IsValid)
        {
            TempData["SiteError"] = "Site name is required.";
            return RedirectToAction(nameof(SiteManagement));
        }

        var site = _siteService.CreateSite(model.Name);
        if (site is null)
            TempData["SiteError"] = "Unable to create site. Check the 50-site limit and site name.";
        else
            TempData["SiteSuccess"] = $"Created site '{site.Name}'.";

        return RedirectToAction(nameof(SiteManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RenameSite(RenameSiteViewModel model)
    {
        if (!IsCoordinator())
            return Forbid();

        if (!ModelState.IsValid)
        {
            TempData["SiteError"] = "New site name is required.";
            return RedirectToAction(nameof(SiteManagement));
        }

        if (_siteService.RenameSite(model.SiteId, model.NewName))
        {
            var site = _siteService.GetSite(model.SiteId);
            TempData["SiteSuccess"] = $"Updated site to '{site?.Name}'.";
        }
        else
        {
            TempData["SiteError"] = "Unable to rename site.";
        }

        return RedirectToAction(nameof(SiteManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeactivateSite(int siteId)
    {
        if (!IsCoordinator())
            return Forbid();

        TempData[_siteService.DeactivateSite(siteId) ? "SiteSuccess" : "SiteError"] =
            _siteService.GetSite(siteId) is { } site && !site.IsActive
                ? $"Deactivated site '{site.Name}'."
                : "Unable to deactivate site.";

        return RedirectToAction(nameof(SiteManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ActivateSite(int siteId)
    {
        if (!IsCoordinator())
            return Forbid();

        TempData[_siteService.ActivateSite(siteId) ? "SiteSuccess" : "SiteError"] =
            _siteService.GetSite(siteId) is { } site && site.IsActive
                ? $"Activated site '{site.Name}'."
                : "Unable to activate site.";

        return RedirectToAction(nameof(SiteManagement));
    }

    private SiteManagementViewModel BuildSiteManagementViewModel() => new()
    {
        Sites = _siteService.GetAllSites()
    };

    private bool IsCoordinator() => bool.TryParse(User.FindFirstValue("IsCoordinator"), out var isCoordinator) && isCoordinator;
}
