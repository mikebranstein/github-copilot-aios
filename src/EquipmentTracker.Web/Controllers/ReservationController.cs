using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Handles project-based equipment reservation, scheduling calendar, and
/// cross-site availability views.
/// Added for Issue #123 - Project-Based Equipment Reservation &amp; Scheduling Calendar.
/// </summary>
[Authorize]
public class ReservationController : Controller
{
    private readonly IReservationService _reservationService;
    private readonly IEquipmentService _equipmentService;
    private readonly IReservationNotificationService _notificationService;

    // ±2-week default window; max 90-day forward look-ahead
    private static readonly int DefaultWindowDays = 14;
    private static readonly int MaxForwardDays = 90;

    // Hard-coded site list for MVP (no separate site catalog required by design)
    private static readonly List<SiteOption> KnownSites = new()
    {
        new SiteOption { SiteId = 1, SiteName = "Site 1" },
        new SiteOption { SiteId = 2, SiteName = "Site 2" },
        new SiteOption { SiteId = 3, SiteName = "Site 3" }
    };

    public ReservationController(
        IReservationService reservationService,
        IEquipmentService equipmentService,
        IReservationNotificationService notificationService)
    {
        _reservationService = reservationService;
        _equipmentService = equipmentService;
        _notificationService = notificationService;
    }

    // ── Index (my reservations + notifications) ──────────────────────────────

    [HttpGet]
    public IActionResult Index()
    {
        var userId = GetUserId();
        var allReservations = _reservationService.GetAllProjectReservationsForUser(userId);
        var notifications = _notificationService.GetNotificationsForUser(userId);

        var vm = new ReservationListViewModel
        {
            MyReservations = allReservations,
            Notifications = notifications
        };

        return View(vm);
    }

    // ── Create reservation ───────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Create(int? projectId = null, int? equipmentId = null)
    {
        var vm = BuildCreateViewModel();
        if (projectId.HasValue) vm.ProjectId = projectId;
        if (equipmentId.HasValue) vm.EquipmentId = equipmentId.Value;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(CreateReservationViewModel vm)
    {
        // Reject past start dates
        if (vm.StartDate < DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(vm.StartDate), "Reservation start date cannot be in the past.");

        if (vm.EndDate < vm.StartDate)
            ModelState.AddModelError(nameof(vm.EndDate), "End date must be on or after start date.");

        if (vm.EndDate > DateOnly.FromDateTime(DateTime.Today).AddDays(MaxForwardDays))
            ModelState.AddModelError(nameof(vm.EndDate), $"Reservations cannot extend beyond {MaxForwardDays} days from today.");

        if (!ModelState.IsValid)
        {
            // Repopulate options
            var rebuilt = BuildCreateViewModel();
            vm.AvailableProjects = rebuilt.AvailableProjects;
            vm.AvailableEquipment = rebuilt.AvailableEquipment;
            vm.AvailableSites = rebuilt.AvailableSites;
            return View(vm);
        }

        var userId = GetUserId();
        var userName = GetUserName();

        // Inline project creation
        int resolvedProjectId = vm.ProjectId ?? 0;
        if (resolvedProjectId == 0 && !string.IsNullOrWhiteSpace(vm.NewProjectName))
        {
            var newProject = _reservationService.CreateProject(
                vm.NewProjectName!,
                vm.NewProjectStartDate ?? vm.StartDate,
                vm.NewProjectEndDate ?? vm.EndDate,
                vm.NewProjectSiteId ?? 0,
                KnownSites.FirstOrDefault(s => s.SiteId == vm.NewProjectSiteId)?.SiteName ?? "Unknown",
                userId, userName);
            resolvedProjectId = newProject.Id;
        }

        if (resolvedProjectId == 0)
        {
            ModelState.AddModelError(nameof(vm.ProjectId), "Please select or create a project.");
            var rebuilt = BuildCreateViewModel();
            vm.AvailableProjects = rebuilt.AvailableProjects;
            vm.AvailableEquipment = rebuilt.AvailableEquipment;
            vm.AvailableSites = rebuilt.AvailableSites;
            return View(vm);
        }

        var (reservation, conflicts) = _reservationService.TryCreateReservation(
            resolvedProjectId, vm.EquipmentId, vm.StartDate, vm.EndDate, userId, userName);

        if (conflicts.Count > 0)
        {
            // Show conflict page with alternatives
            var item = _equipmentService.GetItem(vm.EquipmentId);
            var project = _reservationService.GetProject(resolvedProjectId);
            var conflictVm = new ReservationConflictViewModel
            {
                OriginalRequest = vm,
                Conflicts = conflicts,
                CanOverride = IsOperationsManager() || IsSafetyAdmin(),
                RequestedEquipmentName = item?.Name ?? string.Empty,
                RequestedProjectName = project?.Name ?? string.Empty
            };
            // Carry resolved project ID back into the form
            conflictVm.OriginalRequest.ProjectId = resolvedProjectId;
            return View("Conflict", conflictVm);
        }

        return RedirectToAction(nameof(Details), new { id = reservation!.Id });
    }

    // ── Override (Operations Manager only) ──────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Override(CreateReservationViewModel vm)
    {
        if (!IsOperationsManager() && !IsSafetyAdmin())
            return Forbid();

        if (vm.StartDate < DateOnly.FromDateTime(DateTime.Today))
            return BadRequest("Cannot create reservation with past start date.");

        var userId = GetUserId();
        var userName = GetUserName();

        int resolvedProjectId = vm.ProjectId ?? 0;

        var reservation = _reservationService.CreateReservationWithOverride(
            resolvedProjectId, vm.EquipmentId, vm.StartDate, vm.EndDate, userId, userName, userId);

        return RedirectToAction(nameof(Details), new { id = reservation.Id });
    }

    // ── Details ──────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Details(int id)
    {
        var reservation = _reservationService.GetReservation(id);
        if (reservation == null) return NotFound();

        var userId = GetUserId();
        bool isOpsManager = IsOperationsManager() || IsSafetyAdmin();

        var vm = new ReservationDetailsViewModel
        {
            Reservation = reservation,
            Project = _reservationService.GetProject(reservation.ProjectId),
            CanEdit = reservation.Status == ReservationStatus.Active
                && (reservation.CreatedByUserId == userId || isOpsManager),
            CanCancel = reservation.Status == ReservationStatus.Active
                && (reservation.CreatedByUserId == userId || isOpsManager),
            CanOverride = isOpsManager
        };

        return View(vm);
    }

    // ── Edit ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var reservation = _reservationService.GetReservation(id);
        if (reservation == null) return NotFound();

        var userId = GetUserId();
        if (reservation.CreatedByUserId != userId && !IsOperationsManager() && !IsSafetyAdmin())
            return Forbid();

        var vm = new EditReservationViewModel
        {
            ReservationId = id,
            EquipmentId = reservation.EquipmentId,
            StartDate = reservation.StartDate,
            EndDate = reservation.EndDate,
            AvailableEquipment = _equipmentService.GetAllItems()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(EditReservationViewModel vm)
    {
        if (vm.StartDate < DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(vm.StartDate), "Start date cannot be in the past.");

        if (!ModelState.IsValid)
        {
            vm.AvailableEquipment = _equipmentService.GetAllItems();
            return View(vm);
        }

        var userId = GetUserId();
        var reservation = _reservationService.GetReservation(vm.ReservationId);
        if (reservation == null) return NotFound();

        if (reservation.CreatedByUserId != userId && !IsOperationsManager() && !IsSafetyAdmin())
            return Forbid();

        var (updated, conflicts) = _reservationService.TryEditReservation(
            vm.ReservationId, vm.StartDate, vm.EndDate, vm.EquipmentId, userId);

        if (!updated && conflicts.Count > 0)
        {
            var item = _equipmentService.GetItem(vm.EquipmentId);
            var conflictVm = new ReservationConflictViewModel
            {
                Conflicts = conflicts,
                CanOverride = IsOperationsManager() || IsSafetyAdmin(),
                RequestedEquipmentName = item?.Name ?? string.Empty
            };
            return View("Conflict", conflictVm);
        }

        return RedirectToAction(nameof(Details), new { id = vm.ReservationId });
    }

    // ── Cancel ───────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Cancel(int id)
    {
        var userId = GetUserId();
        bool isOpsManager = IsOperationsManager() || IsSafetyAdmin();
        var cancelled = _reservationService.CancelReservation(id, userId, isOpsManager);
        if (!cancelled) return Forbid();
        return RedirectToAction(nameof(Index));
    }

    // ── Calendar view ─────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Calendar(int? siteId = null, int? projectId = null, string? from = null, string? to = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var fromDate = from != null && DateOnly.TryParse(from, out var parsedFrom)
            ? parsedFrom : today.AddDays(-DefaultWindowDays);

        var toDate = to != null && DateOnly.TryParse(to, out var parsedTo)
            ? parsedTo : today.AddDays(DefaultWindowDays);

        // Enforce 90-day forward cap from today
        if (toDate > today.AddDays(MaxForwardDays))
            toDate = today.AddDays(MaxForwardDays);

        // Site supervisors with a specific site assigned see their site by default
        int userId = GetUserId();
        if (siteId == null && !IsOperationsManager() && !IsSafetyAdmin())
        {
            var claims = User.Claims.FirstOrDefault(c => c.Type == "SiteId");
            if (claims != null && int.TryParse(claims.Value, out var userSiteId) && userSiteId > 0)
                siteId = userSiteId;
        }

        var reservations = _reservationService.GetCalendarReservations(fromDate, toDate, siteId, projectId);

        // Group by equipment for timeline rows
        var equipmentRows = reservations
            .GroupBy(r => new { r.EquipmentId, r.EquipmentName, r.EquipmentCategory })
            .Select(g => new CalendarEquipmentRow
            {
                EquipmentId = g.Key.EquipmentId,
                EquipmentName = g.Key.EquipmentName,
                Category = g.Key.EquipmentCategory,
                Reservations = g.ToList()
            })
            .OrderBy(r => r.Category).ThenBy(r => r.EquipmentName)
            .ToList();

        var vm = new CalendarViewModel
        {
            From = fromDate,
            To = toDate,
            FilterSiteId = siteId,
            FilterProjectId = projectId,
            Reservations = reservations,
            AvailableProjects = _reservationService.GetAllProjects(),
            AvailableSites = KnownSites,
            EquipmentRows = equipmentRows
        };

        return View(vm);
    }

    // ── Cross-site availability ───────────────────────────────────────────────

    [HttpGet]
    public IActionResult CrossSite(int? siteId = null, int windowDays = 14)
    {
        if (windowDays <= 0 || windowDays > MaxForwardDays) windowDays = DefaultWindowDays;

        // Site supervisors default to their own site unless overriding
        if (siteId == null && !IsOperationsManager() && !IsSafetyAdmin())
        {
            var claims = User.Claims.FirstOrDefault(c => c.Type == "SiteId");
            if (claims != null && int.TryParse(claims.Value, out var userSiteId) && userSiteId > 0)
                siteId = userSiteId;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = today;
        var to = today.AddDays(windowDays - 1);

        var summaries = _reservationService.GetCrossSiteAvailability(from, to, siteId);

        var vm = new CrossSiteViewModel
        {
            From = from,
            To = to,
            WindowDays = windowDays,
            FilterSiteId = siteId,
            Summaries = summaries,
            AvailableSites = KnownSites
        };

        return View(vm);
    }

    // ── Notifications ─────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult MarkNotificationRead(int notificationId)
    {
        _notificationService.MarkRead(notificationId, GetUserId());
        return RedirectToAction(nameof(Index));
    }

    // ── Projects ──────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateProject(string name, DateOnly startDate, DateOnly endDate, int siteId)
    {
        var userId = GetUserId();
        var userName = GetUserName();
        var siteName = KnownSites.FirstOrDefault(s => s.SiteId == siteId)?.SiteName ?? "Unknown";
        var project = _reservationService.CreateProject(name, startDate, endDate, siteId, siteName, userId, userName);
        return RedirectToAction(nameof(Create), new { projectId = project.Id });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private int GetUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private string GetUserName() =>
        User.FindFirstValue(ClaimTypes.Name) ?? "Unknown";

    private bool IsOperationsManager() =>
        User.HasClaim("IsOperationsManager", "true")
        || User.IsInRole("OperationsManager");

    private bool IsSafetyAdmin() =>
        User.HasClaim("IsSafetyAdmin", "true")
        || User.IsInRole("SafetyAdmin");

    private CreateReservationViewModel BuildCreateViewModel() => new()
    {
        AvailableProjects = _reservationService.GetAllProjects(),
        AvailableEquipment = _equipmentService.GetAllItems(),
        AvailableSites = KnownSites
    };
}
