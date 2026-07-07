using EquipmentTracker.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EquipmentTracker.Web.Controllers;

[Authorize]
public class CoordinatorQueueDashboardController : Controller
{
    private readonly IWaitlistService _waitlistService;
    private readonly IEquipmentService _equipmentService;

    public CoordinatorQueueDashboardController(IWaitlistService waitlistService, IEquipmentService equipmentService)
    {
        _waitlistService = waitlistService;
        _equipmentService = equipmentService;
    }

    public async Task<IActionResult> Index()
    {
        var queues = await _waitlistService.GetAllActiveQueuesAsync();
        ViewBag.Queues = queues;
        ViewBag.EquipmentService = _equipmentService;
        return View();
    }

    public async Task<IActionResult> Detail(int equipmentItemId)
    {
        var item = _equipmentService.GetItem(equipmentItemId);
        if (item is null) return NotFound();
        var queue = await _waitlistService.GetQueueForItemAsync(equipmentItemId);
        var history = await _waitlistService.GetHistoryForItemAsync(equipmentItemId);
        ViewBag.Item = item;
        ViewBag.Queue = queue;
        ViewBag.History = history;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPosition(int entryId, int newPosition, string reason)
    {
        var coordinatorName = User.FindFirstValue(ClaimTypes.Name) ?? "Coordinator";
        var entry = await _waitlistService.GetEntryAsync(entryId);
        if (entry is null) return NotFound();
        await _waitlistService.OverridePositionAsync(entryId, newPosition, reason, coordinatorName);
        TempData["SuccessMessage"] = "Position updated.";
        return RedirectToAction("Detail", new { equipmentItemId = entry.EquipmentItemId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveEntry(int entryId, string reason)
    {
        var coordinatorName = User.FindFirstValue(ClaimTypes.Name) ?? "Coordinator";
        var entry = await _waitlistService.GetEntryAsync(entryId);
        if (entry is null) return NotFound();
        var itemId = entry.EquipmentItemId;
        await _waitlistService.RemoveEntryAsync(entryId, reason, coordinatorName);
        TempData["SuccessMessage"] = "Entry removed.";
        return RedirectToAction("Detail", new { equipmentItemId = itemId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkUrgent(int entryId)
    {
        var coordinatorName = User.FindFirstValue(ClaimTypes.Name) ?? "Coordinator";
        var entry = await _waitlistService.GetEntryAsync(entryId);
        if (entry is null) return NotFound();
        await _waitlistService.MarkUrgentAsync(entryId, coordinatorName);
        TempData["SuccessMessage"] = "Entry marked as urgent.";
        return RedirectToAction("Detail", new { equipmentItemId = entry.EquipmentItemId });
    }

    public IActionResult AuditLog(int equipmentItemId)
    {
        var item = _equipmentService.GetItem(equipmentItemId);
        if (item is null) return NotFound();
        var log = _waitlistService.GetAuditLog(equipmentItemId);
        ViewBag.Item = item;
        ViewBag.AuditLog = log;
        return View();
    }
}
