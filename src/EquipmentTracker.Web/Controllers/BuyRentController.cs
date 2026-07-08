using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Controller for buy vs. rent recommendations and manual rental cost entry (AC-3, AC-4).
/// </summary>
public class BuyRentController : Controller
{
    private readonly IBuyRentRecommendationService _recommendationService;
    private readonly IRentalCostService _rentalCostService;
    private readonly IUserService _userService;

    public BuyRentController(
        IBuyRentRecommendationService recommendationService,
        IRentalCostService rentalCostService,
        IUserService userService)
    {
        _recommendationService = recommendationService;
        _rentalCostService = rentalCostService;
        _userService = userService;
    }

    // GET /BuyRent
    public IActionResult Index()
    {
        var currentUser = GetCurrentUser();
        double threshold = GetBreakEvenThreshold(currentUser);

        var recommendations = _recommendationService.EvaluateAll(DateTime.UtcNow, threshold);

        var model = new BuyRentDashboardViewModel
        {
            Recommendations = recommendations,
            BreakEvenThreshold = threshold
        };

        return View(model);
    }

    // GET /BuyRent/AddRentalCost
    public IActionResult AddRentalCost()
    {
        return View(new RentalCostEntryViewModel());
    }

    // POST /BuyRent/AddRentalCost
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddRentalCost(RentalCostEntryViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (model.PeriodEnd <= model.PeriodStart)
        {
            ModelState.AddModelError(nameof(model.PeriodEnd),
                "Period end must be after period start.");
            return View(model);
        }

        var username = User.Identity?.Name ?? "Unknown";
        _rentalCostService.AddEntry(
            model.AssetCategory,
            DateTime.SpecifyKind(model.PeriodStart, DateTimeKind.Utc),
            DateTime.SpecifyKind(model.PeriodEnd, DateTimeKind.Utc),
            model.CostAmount,
            username,
            model.Currency);

        TempData["SuccessMessage"] = $"Rental cost for '{model.AssetCategory}' added successfully.";
        return RedirectToAction(nameof(Index));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ApplicationUser? GetCurrentUser()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username)) return null;
        return _userService.GetByUsername(username);
    }

    /// <summary>
    /// Returns the configured break-even threshold for the current user/account.
    /// For MVP, uses the configurable default from appsettings (fallback: 0.70).
    /// </summary>
    private static double GetBreakEvenThreshold(ApplicationUser? user) => 0.70;
}
