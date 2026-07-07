using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EquipmentTracker.Web.Controllers;

[Authorize]
public class WaitlistController : Controller
{
    private readonly IWaitlistService _waitlistService;
    private readonly IEquipmentService _equipmentService;

    public WaitlistController(IWaitlistService waitlistService, IEquipmentService equipmentService)
    {
        _waitlistService = waitlistService;
        _equipmentService = equipmentService;
    }

    [HttpGet]
    public IActionResult JoinQueue(int equipmentItemId)
    {
        var item = _equipmentService.GetItem(equipmentItemId);
        if (item is null) return NotFound();
        ViewBag.Item = item;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> JoinQueue(int equipmentItemId, string? tier)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? "Unknown";
        var parsedTier = tier == "Urgent" ? WaitlistTier.Urgent : WaitlistTier.Standard;
        var entry = await _waitlistService.JoinQueueAsync(equipmentItemId, userId, userName, parsedTier);
        return RedirectToAction("Position", new { entryId = entry.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Position(int entryId)
    {
        var entry = await _waitlistService.GetEntryAsync(entryId);
        if (entry is null) return NotFound();
        var (position, eta) = await _waitlistService.GetPositionAndEtaAsync(entryId);
        var item = _equipmentService.GetItem(entry.EquipmentItemId);
        ViewBag.Entry = entry;
        ViewBag.Position = position;
        ViewBag.Eta = eta;
        ViewBag.Item = item;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int entryId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var success = await _waitlistService.CancelEntryAsync(entryId, userId);
        if (!success)
        {
            TempData["ErrorMessage"] = "Could not cancel entry. You may only cancel your own waitlist entry.";
            return RedirectToAction("Position", new { entryId });
        }

        TempData["SuccessMessage"] = "Waitlist entry cancelled.";
        return RedirectToAction("Index", "Equipment");
    }

    [HttpGet]
    public async Task<IActionResult> Confirm(int entryId)
    {
        var entry = await _waitlistService.GetEntryAsync(entryId);
        if (entry is null) return NotFound();
        var item = _equipmentService.GetItem(entry.EquipmentItemId);
        ViewBag.Entry = entry;
        ViewBag.Item = item;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPost(int entryId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var success = await _waitlistService.ConfirmReservationAsync(entryId, userId);
        if (!success)
        {
            TempData["ErrorMessage"] = "Could not confirm reservation.";
            return RedirectToAction("Confirm", new { entryId });
        }

        TempData["SuccessMessage"] = "Reservation confirmed! Please proceed to collect your equipment.";
        return RedirectToAction("Index", "Equipment");
    }

    [HttpGet]
    public async Task<IActionResult> Alternatives(int equipmentItemId)
    {
        var item = _equipmentService.GetItem(equipmentItemId);
        if (item is null) return NotFound();
        var alternatives = await _waitlistService.GetAlternativesForCategoryAsync(equipmentItemId);
        ViewBag.Item = item;
        ViewBag.Alternatives = alternatives;
        return View();
    }
}
