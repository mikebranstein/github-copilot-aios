using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Controller for the utilization dashboard (AC-1, AC-2).
/// Fleet Manager, Operations Director, CFO, and Admin roles can view.
/// </summary>
public class UtilizationDashboardController : Controller
{
    private readonly IUtilizationService _utilizationService;
    private readonly IUserService _userService;

    public UtilizationDashboardController(
        IUtilizationService utilizationService,
        IUserService userService)
    {
        _utilizationService = utilizationService;
        _userService = userService;
    }

    // GET /UtilizationDashboard
    public IActionResult Index()
    {
        var currentUser = GetCurrentUser();

        // Any authenticated user may access the dashboard; role determines export button visibility.
        var metrics = _utilizationService.GetFleetUtilization(DateTime.UtcNow);

        var model = new FleetUtilizationViewModel
        {
            Assets = metrics,
            CanExportCfoReport = currentUser?.CanExportCfoReport ?? false
        };

        return View(model);
    }

    // GET /UtilizationDashboard/Asset/5
    public IActionResult Asset(int id)
    {
        var metrics = _utilizationService.GetAssetUtilization(id, DateTime.UtcNow);
        if (metrics is null)
            return NotFound();

        var model = new AssetUtilizationViewModel { Metrics = metrics };
        return View(model);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private ApplicationUser? GetCurrentUser()
    {
        var usernameClaim = User.Identity?.Name;
        if (string.IsNullOrEmpty(usernameClaim)) return null;
        return _userService.GetByUsername(usernameClaim);
    }
}
