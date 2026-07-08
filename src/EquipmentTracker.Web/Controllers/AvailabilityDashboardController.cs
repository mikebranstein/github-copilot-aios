using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Serves the real-time equipment availability dashboard for field managers.
/// Provides read-only availability views, soft hold management, and Notify Me subscriptions.
/// Added for Issue #118 — Real-Time Equipment Availability Dashboard.
/// </summary>
[Authorize]
public class AvailabilityDashboardController : Controller
{
    private readonly IAvailabilityDashboardService _dashboardService;
    private readonly ISoftHoldService _softHoldService;
    private readonly INotifyMeService _notifyMeService;
    private readonly IUserPreferencesService _preferencesService;
    private readonly IUserService _userService;

    public AvailabilityDashboardController(
        IAvailabilityDashboardService dashboardService,
        ISoftHoldService softHoldService,
        INotifyMeService notifyMeService,
        IUserPreferencesService preferencesService,
        IUserService userService)
    {
        _dashboardService = dashboardService;
        _softHoldService = softHoldService;
        _notifyMeService = notifyMeService;
        _preferencesService = preferencesService;
        _userService = userService;
    }

    // GET /AvailabilityDashboard
    [HttpGet]
    public IActionResult Index(string? site = null, string? category = null)
    {
        var userId = GetCurrentUserId();
        var currentUser = userId.HasValue ? _userService.GetById(userId.Value) : null;

        // Site filter: use query param → persisted pref → assigned site
        var prefs = userId.HasValue ? _preferencesService.GetPreferences(userId.Value) : null;
        var effectiveSite = site
            ?? prefs?.PreferredSiteFilter
            ?? currentUser?.AssignedSite;

        var effectiveCategory = category
            ?? prefs?.PreferredCategoryFilter;

        // Persist the selection
        if (userId.HasValue)
            _preferencesService.SavePreferences(userId.Value, effectiveSite, effectiveCategory);

        var vm = _dashboardService.GetDashboard(effectiveSite, effectiveCategory);
        vm.SiteFilter = effectiveSite;
        vm.CategoryFilter = effectiveCategory;

        if (userId.HasValue)
            vm.MySubscriptions = _notifyMeService.GetActiveSubscriptionsForUser(userId.Value);

        return View(vm);
    }

    // POST /AvailabilityDashboard/PlaceHold
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceHold(int equipmentItemId, string? returnSite, string? returnCategory)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var hold = await _softHoldService.PlaceHoldAsync(equipmentItemId, userId.Value);

        if (hold is null)
        {
            TempData["HoldError"] = "This item was just claimed by another user.";
        }
        else
        {
            TempData["HoldSuccess"] = $"Soft hold placed. Item reserved for {hold.RemainingMinutes()} minutes.";
        }

        return RedirectToAction(nameof(Index), new { site = returnSite, category = returnCategory });
    }

    // POST /AvailabilityDashboard/ReleaseHold
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReleaseHold(int holdId, string? returnSite, string? returnCategory)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var released = await _softHoldService.ReleaseHoldAsync(holdId, userId.Value);

        TempData[released ? "HoldSuccess" : "HoldError"] =
            released ? "Hold released." : "Hold not found or you are not the owner.";

        return RedirectToAction(nameof(Index), new { site = returnSite, category = returnCategory });
    }

    // POST /AvailabilityDashboard/SubscribeItem
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubscribeItem(int equipmentItemId, string? returnSite, string? returnCategory)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        await _notifyMeService.SubscribeToItemAsync(userId.Value, equipmentItemId);
        TempData["NotifySuccess"] = "Notify Me subscription created. You will be alerted when this item becomes available.";

        return RedirectToAction(nameof(Index), new { site = returnSite, category = returnCategory });
    }

    // POST /AvailabilityDashboard/SubscribeCategory
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubscribeCategory(string category, string? returnSite, string? returnCategory)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        await _notifyMeService.SubscribeToCategoryAsync(userId.Value, category);
        TempData["NotifySuccess"] = $"Notify Me subscription created for category '{category}'.";

        return RedirectToAction(nameof(Index), new { site = returnSite, category = returnCategory });
    }

    // POST /AvailabilityDashboard/CancelSubscription
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSubscription(int subscriptionId, string? returnSite, string? returnCategory)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        await _notifyMeService.CancelSubscriptionAsync(subscriptionId, userId.Value);
        TempData["NotifySuccess"] = "Notify Me subscription cancelled.";

        return RedirectToAction(nameof(Index), new { site = returnSite, category = returnCategory });
    }

    // GET /AvailabilityDashboard/Refresh (AJAX endpoint for 5-minute auto-refresh)
    [HttpGet]
    public IActionResult Refresh(string? site = null, string? category = null)
    {
        var vm = _dashboardService.GetDashboard(site, category);
        return PartialView("_DashboardContent", vm);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private int? GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out var id) ? id : null;
    }
}
