using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Handles the mobile checkout flow: QR scan → assignment → confirm.
/// Routes: /mobile/checkout/*
/// </summary>
[Authorize]
[Route("mobile/checkout")]
public class MobileCheckoutController : Controller
{
    private readonly IEquipmentService _equipmentService;
    private readonly IUserService _userService;
    private readonly ICoordinatorNotificationService _notificationService;
    private readonly IPushNotificationService _pushService;
    private readonly IApprovalService _approvalService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MobileCheckoutController> _logger;

    public MobileCheckoutController(
        IEquipmentService equipmentService,
        IUserService userService,
        ICoordinatorNotificationService notificationService,
        IPushNotificationService pushService,
        IApprovalService approvalService,
        IConfiguration configuration,
        ILogger<MobileCheckoutController> logger)
    {
        _equipmentService = equipmentService;
        _userService = userService;
        _notificationService = notificationService;
        _pushService = pushService;
        _approvalService = approvalService;
        _configuration = configuration;
        _logger = logger;
    }

    // GET /mobile/checkout/scan
    [HttpGet("scan")]
    public IActionResult Scan()
    {
        return View();
    }

    // GET /mobile/checkout/lookup?code={qr_value}
    // Returns equipment item JSON for the given QR/barcode code.
    // Matches by item ID (string parse) or item name (case-insensitive).
    [HttpGet("lookup")]
    public IActionResult Lookup([FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "code is required" });

        EquipmentItem? item = null;

        // Try numeric ID first
        if (int.TryParse(code, out var id))
            item = _equipmentService.GetItem(id);

        // Fall back to name search
        if (item is null)
        {
            item = _equipmentService.GetAllItems()
                .FirstOrDefault(i => i.Name.Equals(code, StringComparison.OrdinalIgnoreCase));
        }

        if (item is null)
            return NotFound(new { error = $"No equipment found for code '{code}'" });

        return Json(new
        {
            id = item.Id,
            name = item.Name,
            category = item.Category,
            isAvailable = item.IsAvailable
        });
    }

    // GET /mobile/checkout/confirm?itemId={id}&assigneeId={userId}
    [HttpGet("confirm")]
    public IActionResult Confirm([FromQuery] int itemId, [FromQuery] int assigneeId = 0)
    {
        var item = _equipmentService.GetItem(itemId);
        if (item is null) return NotFound();

        var borrowers = _userService.GetBorrowers();

        var vm = new MobileCheckoutConfirmViewModel
        {
            Item = item,
            AssigneeId = assigneeId,
            Borrowers = borrowers
        };

        // Show conflict info if item is already checked out
        if (!item.IsAvailable)
        {
            var activeRecord = _equipmentService.GetActiveCheckoutRecord(itemId);
            vm.ErrorMessage = activeRecord is not null
                ? $"This item is currently checked out by {activeRecord.BorrowerName} (checked out {activeRecord.CheckedOutAtUtc:g} UTC)."
                : "This item is not available.";
        }

        return View(vm);
    }

    // POST /mobile/checkout/confirm
    [HttpPost("confirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPost(
        [FromForm] int itemId,
        [FromForm] int assigneeId,
        [FromForm] string assigneeName,
        [FromForm] string? conditionNote)
    {
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var currentUsername = User.FindFirstValue(ClaimTypes.Name) ?? "unknown";

        // Resolve the assignee
        int effectiveUserId = assigneeId > 0 ? assigneeId : currentUserId;
        string effectiveName = assigneeName;

        if (string.IsNullOrWhiteSpace(effectiveName))
        {
            var assigneeUser = _userService.GetById(effectiveUserId);
            effectiveName = assigneeUser?.Username ?? currentUsername;
        }

        // Enforce max condition note length
        if (conditionNote?.Length > 500)
            conditionNote = conditionNote[..500];

        var item = _equipmentService.GetItem(itemId);
        if (item is null)
            return NotFound();

        // Server-side idempotency: same user+item within 60 seconds → return existing
        if (!item.IsAvailable && _equipmentService.IsIdempotentCheckout(itemId, effectiveUserId))
        {
            TempData["SuccessMessage"] = $"'{item.Name}' is already checked out to you (duplicate request detected).";
            return RedirectToAction("Success", new { itemId });
        }

        // Availability conflict
        if (!item.IsAvailable)
        {
            var activeRecord = _equipmentService.GetActiveCheckoutRecord(itemId);
            var borrowers = _userService.GetBorrowers();
            var vm = new MobileCheckoutConfirmViewModel
            {
                Item = item,
                AssigneeId = assigneeId,
                Borrowers = borrowers,
                ErrorMessage = activeRecord is not null
                    ? $"Cannot check out: '{item.Name}' is currently held by {activeRecord.BorrowerName} since {activeRecord.CheckedOutAtUtc:g} UTC."
                    : "Item is unavailable."
            };
            Response.StatusCode = 409;
            return View("Confirm", vm);
        }

        // Perform checkout (with optional condition note)
        var success = _equipmentService.Checkout(itemId, effectiveName, effectiveUserId, conditionNote);
        if (!success)
        {
            var borrowers = _userService.GetBorrowers();
            var vm = new MobileCheckoutConfirmViewModel
            {
                Item = item,
                AssigneeId = assigneeId,
                Borrowers = borrowers,
                ErrorMessage = "Checkout failed. Tap to retry."
            };
            Response.StatusCode = 409;
            return View("Confirm", vm);
        }

        // Create coordinator notifications and optionally send push
        var checkoutRecord = _equipmentService.GetActiveCheckoutRecord(itemId);
        if (checkoutRecord is not null)
        {
            var coordinators = _userService.GetCoordinators();
            var message = $"{effectiveName} checked out '{item.Name}' at {DateTime.UtcNow:g} UTC.";

            foreach (var coordinator in coordinators)
            {
                // Always create in-app notification (persisted in memory regardless of push success)
                _notificationService.CreateNotification(coordinator.Id, checkoutRecord.Id, message);

                // Optionally send push notification
                if (coordinator.NotificationsEnabled && coordinator.PushEndpoint is not null)
                {
                    try
                    {
                        await _pushService.SendAsync(coordinator, "New Equipment Checkout", message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Push notification failed for coordinator {CoordinatorId}. Continuing.", coordinator.Id);
                    }
                }
            }

            // Create approval request
            _approvalService.CreateRequest(checkoutRecord.Id, effectiveUserId);
        }

        TempData["SuccessMessage"] = $"'{item.Name}' successfully checked out to {effectiveName}. Awaiting coordinator approval.";
        return RedirectToAction("Pending", new { checkoutRecordId = checkoutRecord?.Id ?? 0 });
    }

    // GET /mobile/checkout/pending?checkoutRecordId={id}
    [HttpGet("pending")]
    public IActionResult Pending([FromQuery] int checkoutRecordId)
    {
        var approval = _approvalService.GetByCheckoutRecordId(checkoutRecordId);
        ViewBag.CheckoutRecordId = checkoutRecordId;
        ViewBag.InitialStatus = approval?.Status.ToString() ?? "Pending";
        return View();
    }

    // GET /mobile/checkout/approval-status/{checkoutRecordId}
    [HttpGet("approval-status/{checkoutRecordId:int}")]
    public IActionResult ApprovalStatus(int checkoutRecordId)
    {
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var approval = _approvalService.GetByCheckoutRecordId(checkoutRecordId);

        if (approval is null)
            return NotFound(new { error = "No approval request found." });

        // Only the requesting borrower may poll this endpoint
        if (approval.RequestingUserId != currentUserId)
            return Forbid();

        return Json(new
        {
            status = approval.Status.ToString(),
            denialReason = approval.DenialReason
        });
    }

    // GET /mobile/checkout/success?itemId={id}
    [HttpGet("success")]
    public IActionResult Success([FromQuery] int itemId)
    {
        var item = _equipmentService.GetItem(itemId);
        ViewBag.Item = item;
        ViewBag.SuccessMessage = TempData["SuccessMessage"] as string
            ?? $"Checkout confirmed for item #{itemId}.";
        return View();
    }
}
