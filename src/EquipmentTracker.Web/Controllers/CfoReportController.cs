using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Controller for the CFO Executive Report export (AC-5, AC-6, AC-7).
///
/// RBAC enforcement: CFO and Admin roles only. Role check is performed server-side
/// on the GenerateReport action — hiding the button in the UI alone is insufficient.
/// </summary>
public class CfoReportController : Controller
{
    private readonly ICfoReportService _reportService;
    private readonly IUserService _userService;

    public CfoReportController(ICfoReportService reportService, IUserService userService)
    {
        _reportService = reportService;
        _userService   = userService;
    }

    // GET /CfoReport
    public IActionResult Index()
    {
        var currentUser = GetCurrentUser();
        bool authorized = currentUser?.CanExportCfoReport ?? false;

        var model = new CfoReportViewModel
        {
            IsAuthorized = authorized
        };

        return View(model);
    }

    // GET /CfoReport/GenerateReport
    public IActionResult GenerateReport()
    {
        var currentUser = GetCurrentUser();

        // AC-7: Server-side RBAC — return 403 for unauthorized roles.
        if (currentUser is null || !currentUser.CanExportCfoReport)
        {
            return Forbid();
        }

        var companyName = "Your Company";  // Could be sourced from account settings in v2
        var reportData  = _reportService.GenerateReport(DateTime.UtcNow, companyName);

        var model = new CfoReportViewModel
        {
            ReportData   = reportData,
            IsAuthorized = true
        };

        return View("Report", model);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private ApplicationUser? GetCurrentUser()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username)) return null;
        return _userService.GetByUsername(username);
    }
}
