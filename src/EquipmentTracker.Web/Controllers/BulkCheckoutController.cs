using EquipmentTracker.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Coordinator-only bulk checkout flow.
/// Routes: /mobile/bulk-checkout/*
/// </summary>
[Authorize]
[Route("mobile/bulk-checkout")]
public class BulkCheckoutController : Controller
{
    private readonly IEquipmentService _equipmentService;
    private readonly IUserService _userService;
    private readonly IBulkCheckoutService _bulkCheckoutService;

    public BulkCheckoutController(
        IEquipmentService equipmentService,
        IUserService userService,
        IBulkCheckoutService bulkCheckoutService)
    {
        _equipmentService = equipmentService;
        _userService = userService;
        _bulkCheckoutService = bulkCheckoutService;
    }

    // GET /mobile/bulk-checkout
    [HttpGet("")]
    public IActionResult Index()
    {
        var isCoordinator = bool.TryParse(User.FindFirstValue("IsCoordinator"), out var cv) && cv;
        if (!isCoordinator) return Forbid();

        ViewBag.AvailableItems = _equipmentService.GetAllItems().Where(i => i.IsAvailable).ToList();
        ViewBag.Borrowers = _userService.GetBorrowers();

        return View();
    }

    // POST /mobile/bulk-checkout/confirm
    [HttpPost("confirm")]
    [ValidateAntiForgeryToken]
    public IActionResult Confirm(
        [FromForm] int itemId,
        [FromForm] int borrowerUserId,
        [FromForm] string? conditionNote)
    {
        var isCoordinator = bool.TryParse(User.FindFirstValue("IsCoordinator"), out var cv) && cv;
        if (!isCoordinator) return Forbid();

        var coordinatorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

        var borrower = _userService.GetById(borrowerUserId);
        if (borrower is null)
        {
            TempData["ErrorMessage"] = "Crew member not found.";
            return RedirectToAction("Index");
        }

        // Enforce condition note max length
        if (conditionNote?.Length > 500)
            conditionNote = conditionNote[..500];

        var record = _bulkCheckoutService.BulkCheckout(
            itemId,
            borrowerUserId,
            borrower.Username,
            coordinatorId,
            conditionNote);

        if (record is null)
        {
            TempData["ErrorMessage"] = "Checkout failed — item may be unavailable.";
            return RedirectToAction("Index");
        }

        TempData["SuccessMessage"] = $"Item checked out to {borrower.Username} (coordinator-initiated). Record #{record.Id}.";
        return RedirectToAction("Index");
    }
}
