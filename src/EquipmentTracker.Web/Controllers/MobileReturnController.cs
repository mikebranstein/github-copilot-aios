using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Handles the mobile return flow: QR scan → confirm → done.
/// Routes: /mobile/return/*
/// </summary>
[Authorize]
[Route("mobile/return")]
public class MobileReturnController : Controller
{
    private readonly IEquipmentService _equipmentService;
    private readonly ILogger<MobileReturnController> _logger;

    public MobileReturnController(
        IEquipmentService equipmentService,
        ILogger<MobileReturnController> logger)
    {
        _equipmentService = equipmentService;
        _logger = logger;
    }

    // GET /mobile/return/scan
    [HttpGet("scan")]
    public IActionResult Scan()
    {
        return View();
    }

    // GET /mobile/return/confirm?itemId={id}
    [HttpGet("confirm")]
    public IActionResult Confirm([FromQuery] int itemId)
    {
        var item = _equipmentService.GetItem(itemId);
        if (item is null) return NotFound();

        var activeRecord = _equipmentService.GetActiveCheckoutRecord(itemId);
        string? error = null;

        if (item.IsAvailable)
            error = $"'{item.Name}' is already available — no active checkout to return.";

        var vm = new MobileReturnConfirmViewModel
        {
            Item = item,
            ActiveRecord = activeRecord,
            ErrorMessage = error
        };

        return View(vm);
    }

    // POST /mobile/return/confirm
    [HttpPost("confirm")]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmPost([FromForm] int itemId)
    {
        var item = _equipmentService.GetItem(itemId);
        if (item is null) return NotFound();

        if (item.IsAvailable)
        {
            var vm = new MobileReturnConfirmViewModel
            {
                Item = item,
                ErrorMessage = $"'{item.Name}' is already available."
            };
            Response.StatusCode = 409;
            return View("Confirm", vm);
        }

        var success = _equipmentService.Return(itemId);
        if (!success)
        {
            var vm = new MobileReturnConfirmViewModel
            {
                Item = item,
                ErrorMessage = "Return failed. Tap to retry."
            };
            Response.StatusCode = 409;
            return View("Confirm", vm);
        }

        _logger.LogInformation("Item {ItemId} returned by user {User}.", itemId,
            User.FindFirstValue(ClaimTypes.Name));

        TempData["SuccessMessage"] = $"'{item.Name}' has been returned successfully.";
        return RedirectToAction("Success", new { itemId });
    }

    // GET /mobile/return/success?itemId={id}
    [HttpGet("success")]
    public IActionResult Success([FromQuery] int itemId)
    {
        var item = _equipmentService.GetItem(itemId);
        ViewBag.Item = item;
        ViewBag.SuccessMessage = TempData["SuccessMessage"] as string
            ?? $"Return confirmed for item #{itemId}.";
        return View();
    }
}
