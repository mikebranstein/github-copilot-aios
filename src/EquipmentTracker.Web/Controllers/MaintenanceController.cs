using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Smart Maintenance Scheduling controller — Phase 1 (rule-based, no ML).
/// Covers AC-1 through AC-7 from issue #119.
/// </summary>
[Authorize]
public class MaintenanceController : Controller
{
    private readonly IMaintenanceService _maintenanceService;
    private readonly IEquipmentService _equipmentService;

    public MaintenanceController(IMaintenanceService maintenanceService, IEquipmentService equipmentService)
    {
        _maintenanceService = maintenanceService;
        _equipmentService = equipmentService;
    }

    // ── AC-2: Service Due Dashboard ───────────────────────────────────────────

    [HttpGet]
    public IActionResult Dashboard(string? filterBand, string? filterCategory, int? filterSiteId, string? sortBy)
    {
        var vm = new ServiceDueDashboardViewModel
        {
            Statuses = _maintenanceService.GetAllServiceStatuses(),
            FilterBand = filterBand,
            FilterCategory = filterCategory,
            FilterSiteId = filterSiteId,
            SortBy = sortBy
        };
        return View(vm);
    }

    // ── AC-1 / AC-4: Asset Detail with Usage Hours + Maintenance History ──────

    [HttpGet]
    public IActionResult AssetDetail(int id)
    {
        var item = _equipmentService.GetItem(id);
        if (item is null) return NotFound();

        var vm = new MaintenanceHistoryViewModel
        {
            AssetId = item.Id,
            AssetName = item.Name,
            Category = item.Category,
            OperatingHours = _maintenanceService.GetOperatingHours(id),
            Events = _maintenanceService.GetMaintenanceHistory(id)
        };

        ViewBag.ServiceStatus = _maintenanceService.GetServiceStatus(id);
        ViewBag.HoursToNextService = _maintenanceService.GetHoursToNextService(id);
        ViewBag.ProjectedServiceDate = _maintenanceService.GetProjectedServiceDate(id);
        ViewBag.ServiceInterval = _maintenanceService.GetServiceInterval(item.Category);

        return View(vm);
    }

    // ── AC-4: Log Maintenance Event ───────────────────────────────────────────

    [HttpGet]
    public IActionResult LogEvent(int assetId)
    {
        var item = _equipmentService.GetItem(assetId);
        if (item is null) return NotFound();

        var vm = new LogMaintenanceEventViewModel
        {
            AssetId = assetId,
            EventDate = DateTime.Today,
            HoursAtService = _maintenanceService.GetOperatingHours(assetId)
        };
        ViewBag.AssetName = item.Name;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult LogEvent(LogMaintenanceEventViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var item = _equipmentService.GetItem(model.AssetId);
            ViewBag.AssetName = item?.Name ?? "Unknown";
            return View(model);
        }

        _maintenanceService.LogMaintenanceEvent(
            model.AssetId,
            model.EventType,
            model.EventDate,
            model.HoursAtService,
            model.TechnicianName,
            model.Notes);

        TempData["Success"] = "Maintenance event logged. Service interval counter has been reset.";
        return RedirectToAction(nameof(AssetDetail), new { id = model.AssetId });
    }

    // ── AC-3: Admin — Service Interval Configuration ──────────────────────────

    [HttpGet]
    public IActionResult ServiceIntervals()
    {
        var vm = new ServiceIntervalAdminViewModel
        {
            Intervals = _maintenanceService.GetAllServiceIntervals()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpsertServiceInterval(UpsertServiceIntervalViewModel model)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Category))
        {
            TempData["Error"] = "Category and interval value are required.";
            return RedirectToAction(nameof(ServiceIntervals));
        }

        _maintenanceService.UpsertServiceInterval(model.Category, model.IntervalType, model.IntervalValue, model.LeadTimeDays);
        TempData["Success"] = $"Service interval configured for '{model.Category}'.";
        return RedirectToAction(nameof(ServiceIntervals));
    }

    // ── AC-3: Alert Snooze ────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SnoozeAlert(int assetId, int days)
    {
        if (days != 7 && days != 14 && days != 30)
        {
            TempData["Error"] = "Snooze period must be 7, 14, or 30 days.";
            return RedirectToAction(nameof(Dashboard));
        }

        _maintenanceService.SnoozeAlert(assetId, days);
        TempData["Success"] = $"Alerts for asset snoozed for {days} days.";
        return RedirectToAction(nameof(Dashboard));
    }

    // ── AC-5: Downtime Cost Calculator ────────────────────────────────────────

    [HttpGet]
    public IActionResult DowntimeCostCalculator()
    {
        int enrolledAssets = _equipmentService.GetAllItems()
            .Count(i => _maintenanceService.GetServiceInterval(i.Category) is not null);

        var vm = new DowntimeCostCalculatorViewModel
        {
            AssetCount = enrolledAssets
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DowntimeCostCalculator(DowntimeCostCalculatorViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        model.Result = _maintenanceService.CalculateDowntimeCost(
            model.AssetCount,
            model.DailyCostEstimate,
            model.AvgRepairDays,
            model.EstimatedIncidentsPerYear);

        return View(model);
    }

    // ── AC-6: Phase 1 Gate Metric ─────────────────────────────────────────────

    [HttpGet]
    public IActionResult GateMetric()
    {
        var vm = new MaintenanceGateMetricViewModel
        {
            CompletionRatePercent = _maintenanceService.GetMaintenanceLogCompletionRate()
        };
        return View(vm);
    }
}
