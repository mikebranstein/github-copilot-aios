using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Displays the authenticated user's profile and last 30 checkout/return transactions.
/// Route: /profile
/// </summary>
[Authorize]
[Route("profile")]
public class ProfileController : Controller
{
    private readonly IEquipmentService _equipmentService;
    private readonly IUserService _userService;

    public ProfileController(IEquipmentService equipmentService, IUserService userService)
    {
        _equipmentService = equipmentService;
        _userService = userService;
    }

    // GET /profile
    [HttpGet("")]
    [HttpGet("index")]
    public IActionResult Index()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var user = _userService.GetById(userId);
        var history = _equipmentService.GetCheckoutHistoryByUser(userId, 30);

        var vm = new ProfileViewModel
        {
            Username = user?.Username ?? User.FindFirstValue(ClaimTypes.Name) ?? "unknown",
            IsCoordinator = user?.IsCoordinator ?? false,
            RecentHistory = history
        };

        return View(vm);
    }
}
