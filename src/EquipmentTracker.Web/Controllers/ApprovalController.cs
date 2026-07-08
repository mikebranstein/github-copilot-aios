using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Coordinator approval queue for checkout requests.
/// Routes: /coordinator/approvals/*
/// Extended for Issue #117: emergency override, restricted audit log, denial reason enforcement.
/// </summary>
[Authorize]
[Route("coordinator/approvals")]
public class ApprovalController : Controller
{
    private readonly IApprovalService _approvalService;
    private readonly IEquipmentService _equipmentService;
    private readonly IUserService _userService;

    public ApprovalController(
        IApprovalService approvalService,
        IEquipmentService equipmentService,
        IUserService userService)
    {
        _approvalService = approvalService;
        _equipmentService = equipmentService;
        _userService = userService;
    }

    // GET /coordinator/approvals
    [HttpGet("")]
    public IActionResult Index()
    {
        var isCoordinator = bool.TryParse(User.FindFirstValue("IsCoordinator"), out var cv) && cv;
        if (!isCoordinator) return Forbid();

        var pending = _approvalService.GetPending();

        // Enrich with item and user info
        var enriched = pending.Select(a =>
        {
            var record = _equipmentService.GetCheckoutRecordById(a.CheckoutRecordId);
            var item = record is not null ? _equipmentService.GetItem(record.EquipmentItemId) : null;
            var user = _userService.GetById(a.RequestingUserId);
            return new ApprovalQueueItem
            {
                Request = a,
                ItemName = item?.Name ?? "(unknown)",
                ItemCategory = item?.Category ?? string.Empty,
                RequesterName = user?.Username ?? record?.BorrowerName ?? "(unknown)"
            };
        }).ToList();

        return View(enriched);
    }

    // GET /coordinator/approvals/{id}
    [HttpGet("{id:int}")]
    public IActionResult Details(int id)
    {
        var isCoordinator = bool.TryParse(User.FindFirstValue("IsCoordinator"), out var cv) && cv;
        if (!isCoordinator) return Forbid();

        var request = _approvalService.GetAll().FirstOrDefault(a => a.Id == id);
        if (request is null) return NotFound();

        var record = _equipmentService.GetCheckoutRecordById(request.CheckoutRecordId);
        var item = record is not null ? _equipmentService.GetItem(record.EquipmentItemId) : null;
        var user = _userService.GetById(request.RequestingUserId);

        ViewBag.Request = request;
        ViewBag.ItemName = item?.Name ?? "(unknown)";
        ViewBag.ItemCategory = item?.Category ?? string.Empty;
        ViewBag.RequesterName = user?.Username ?? record?.BorrowerName ?? "(unknown)";
        ViewBag.ConditionNote = record?.ConditionNote;
        ViewBag.IsRestricted = item?.IsRestricted ?? false;

        return View();
    }

    // POST /coordinator/approvals/{id}/approve
    [HttpPost("{id:int}/approve")]
    [ValidateAntiForgeryToken]
    public IActionResult Approve(int id)
    {
        var isCoordinator = bool.TryParse(User.FindFirstValue("IsCoordinator"), out var cv) && cv;
        if (!isCoordinator) return Forbid();

        var coordinatorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var success = _approvalService.Approve(id, coordinatorId);

        if (!success)
        {
            TempData["ErrorMessage"] = "Could not approve request (already decided or not found).";
        }
        else
        {
            TempData["SuccessMessage"] = "Checkout approved.";
        }

        return RedirectToAction("Index");
    }

    // POST /coordinator/approvals/{id}/deny
    [HttpPost("{id:int}/deny")]
    [ValidateAntiForgeryToken]
    public IActionResult Deny(int id, [FromForm] string? reason)
    {
        var isCoordinator = bool.TryParse(User.FindFirstValue("IsCoordinator"), out var cv) && cv;
        if (!isCoordinator) return Forbid();

        // AC-4: Denial reason is mandatory with minimum 10 characters
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
        {
            TempData["ErrorMessage"] = "Denial reason is required and must be at least 10 characters.";
            return RedirectToAction("Details", new { id });
        }

        var coordinatorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var success = _approvalService.Deny(id, coordinatorId, reason);

        if (!success)
        {
            TempData["ErrorMessage"] = "Could not deny request (already decided or not found).";
        }
        else
        {
            TempData["SuccessMessage"] = "Checkout denied and item returned to inventory.";
        }

        return RedirectToAction("Index");
    }

    // POST /coordinator/approvals/{id}/emergency-override
    /// <summary>
    /// AC-8: Emergency Override - Safety Admin only.
    /// Checkout proceeds immediately; immutable audit log entry written with EmergencyOverrideFlag=true.
    /// </summary>
    [HttpPost("{id:int}/emergency-override")]
    [ValidateAntiForgeryToken]
    public IActionResult EmergencyOverride(int id, [FromForm] string? overrideReason)
    {
        var isSafetyAdmin = bool.TryParse(User.FindFirstValue("IsSafetyAdmin"), out var sv) && sv;
        if (!isSafetyAdmin)
        {
            TempData["ErrorMessage"] = "Emergency override requires the Safety Admin role.";
            return RedirectToAction("Details", new { id });
        }

        if (string.IsNullOrWhiteSpace(overrideReason))
        {
            TempData["ErrorMessage"] = "Emergency override reason is mandatory and cannot be empty.";
            return RedirectToAction("Details", new { id });
        }

        var safetyAdminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var success = _approvalService.EmergencyOverride(id, safetyAdminId, overrideReason);

        if (!success)
        {
            TempData["ErrorMessage"] = "Could not apply emergency override (request not found or already decided).";
        }
        else
        {
            TempData["SuccessMessage"] = "Emergency override applied. Checkout proceeds. Audit entry recorded.";
        }

        return RedirectToAction("Index");
    }

    // GET /coordinator/approvals/audit-log
    /// <summary>AC-6: View immutable restricted checkout audit log.</summary>
    [HttpGet("audit-log")]
    public IActionResult AuditLog()
    {
        var isCoordinator = bool.TryParse(User.FindFirstValue("IsCoordinator"), out var cv) && cv;
        if (!isCoordinator) return Forbid();

        var log = _approvalService.GetAuditLog();
        return View(log);
    }
}

/// <summary>View model for the approval queue list.</summary>
public class ApprovalQueueItem
{
    public ApprovalRequest Request { get; set; } = null!;
    public string ItemName { get; set; } = string.Empty;
    public string ItemCategory { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
}
