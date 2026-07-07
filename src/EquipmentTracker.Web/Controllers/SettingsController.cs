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

    public SettingsController(IUserService userService)
    {
        _userService = userService;
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
}
