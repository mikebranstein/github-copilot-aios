using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Shows and manages coordinator in-app notifications.
/// Route: /coordinator/notifications
/// </summary>
[Authorize]
[Route("coordinator/notifications")]
public class CoordinatorNotificationsController : Controller
{
    private readonly ICoordinatorNotificationService _notificationService;
    private readonly IUserService _userService;

    public CoordinatorNotificationsController(
        ICoordinatorNotificationService notificationService,
        IUserService userService)
    {
        _notificationService = notificationService;
        _userService = userService;
    }

    // GET /coordinator/notifications
    [HttpGet("")]
    [HttpGet("index")]
    public IActionResult Index()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var notifications = _notificationService.GetPendingForCoordinator(userId);

        var vm = new CoordinatorNotificationsViewModel
        {
            Notifications = notifications
        };

        return View(vm);
    }

    // POST /coordinator/notifications/{id}/read
    [HttpPost("{id}/read")]
    [ValidateAntiForgeryToken]
    public IActionResult MarkRead(int id)
    {
        _notificationService.MarkRead(id);
        return RedirectToAction(nameof(Index));
    }
}
