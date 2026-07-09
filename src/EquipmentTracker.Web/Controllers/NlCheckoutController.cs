using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Handles the text-based Natural Language checkout flow on mobile.
/// Routes: /mobile/checkout/nl/*
///
/// Issue #149 — Phase 1: Text-Based NL Checkout Interface (Mobile)
///
/// Flow:
///   GET  /mobile/checkout/nl          → NL input screen
///   POST /mobile/checkout/nl/parse    → parse utterance → Success/Ambiguous/Error
///   GET  /mobile/checkout/nl/disambiguate → disambiguation screen
///   POST /mobile/checkout/nl/disambiguate → user selects item → go to NL confirm
///   GET  /mobile/checkout/nl/confirm  → confirmation screen (AC2)
///   POST /mobile/checkout/nl/confirm  → commit transaction
///
/// Non-goals: voice input, equipment return, web checkout, multi-item utterances (all Phase 2).
/// </summary>
[Authorize]
[Route("mobile/checkout/nl")]
public class NlCheckoutController : Controller
{
    private readonly INaturalLanguageCheckoutService _nlService;
    private readonly IEquipmentService _equipmentService;
    private readonly IUserService _userService;
    private readonly ICoordinatorNotificationService _notificationService;
    private readonly IPushNotificationService _pushService;
    private readonly IApprovalService _approvalService;
    private readonly ILogger<NlCheckoutController> _logger;

    public NlCheckoutController(
        INaturalLanguageCheckoutService nlService,
        IEquipmentService equipmentService,
        IUserService userService,
        ICoordinatorNotificationService notificationService,
        IPushNotificationService pushService,
        IApprovalService approvalService,
        ILogger<NlCheckoutController> logger)
    {
        _nlService = nlService;
        _equipmentService = equipmentService;
        _userService = userService;
        _notificationService = notificationService;
        _pushService = pushService;
        _approvalService = approvalService;
        _logger = logger;
    }

    // ── Input screen ──────────────────────────────────────────────────────────

    /// <summary>
    /// GET /mobile/checkout/nl
    /// Renders the NL text input screen.
    /// </summary>
    [HttpGet("")]
    public IActionResult Index()
    {
        return View("Input", new NlCheckoutInputViewModel());
    }

    // ── Parse ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /mobile/checkout/nl/parse
    /// Accepts the raw utterance, runs NL parsing, and routes to the appropriate next step.
    /// AC1: End-to-end in &lt;5 s from NL submission to confirmation prompt displayed.
    /// AC5: Parse failures and LLM timeouts present a clear error message + fallback.
    /// </summary>
    [HttpPost("parse")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Parse([FromForm] string utterance)
    {
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var currentUserName = User.FindFirstValue(ClaimTypes.Name) ?? "unknown";

        if (string.IsNullOrWhiteSpace(utterance))
        {
            return View("Input", new NlCheckoutInputViewModel
            {
                ErrorMessage = "Please enter a checkout request, e.g. 'Check out Drill #4 to me until Friday'."
            });
        }

        NlParseResult parseResult;
        try
        {
            using var cts = new CancellationTokenSource(
                TimeSpan.FromSeconds(NlParseConstants.LlmTimeoutSeconds));
            parseResult = await _nlService.ParseAsync(utterance, currentUserId, currentUserName);
        }
        catch (OperationCanceledException)
        {
            // AC5: LLM timeout → clear error message + one-tap fallback
            _logger.LogWarning("NL parse timed out for user {UserId}", currentUserId);
            return View("Input", new NlCheckoutInputViewModel
            {
                Utterance = utterance,
                ErrorMessage = "Service temporarily unavailable. Please try again or use standard checkout."
            });
        }

        return parseResult.Status switch
        {
            NlParseStatus.Success => RedirectToNlConfirm(parseResult),
            NlParseStatus.Ambiguous => ShowDisambiguationView(parseResult, utterance),
            NlParseStatus.ItemNotFound => View("Input", new NlCheckoutInputViewModel
            {
                Utterance = utterance,
                ErrorMessage = parseResult.ErrorMessage
                    ?? $"Item not found. Check the name and try again, or use standard checkout."
            }),
            NlParseStatus.LowConfidence => View("Input", new NlCheckoutInputViewModel
            {
                Utterance = utterance,
                ErrorMessage = parseResult.ErrorMessage
                    ?? "I didn't understand that — try 'Check out [item] to me until [date]'"
            }),
            NlParseStatus.LlmTimeout or NlParseStatus.ConnectivityError => View("Input",
                new NlCheckoutInputViewModel
                {
                    Utterance = utterance,
                    ErrorMessage = parseResult.ErrorMessage
                        ?? "Service temporarily unavailable. Please try again or use standard checkout."
                }),
            _ => View("Input", new NlCheckoutInputViewModel
            {
                Utterance = utterance,
                ErrorMessage = "Unexpected error. Please try again or use standard checkout."
            })
        };
    }

    // ── Disambiguation ────────────────────────────────────────────────────────

    /// <summary>
    /// POST /mobile/checkout/nl/disambiguate
    /// User selects one item from a list of ambiguous matches.
    /// AC4: Disambiguation prompt → user selects → proceed to confirmation.
    /// </summary>
    [HttpPost("disambiguate")]
    [ValidateAntiForgeryToken]
    public IActionResult Disambiguate(
        [FromForm] int selectedItemId,
        [FromForm] string utterance,
        [FromForm] int assigneeId,
        [FromForm] string? assigneeName,
        [FromForm] string? dueDateIso,
        [FromForm] string? dueDateDisplay)
    {
        var item = _equipmentService.GetItem(selectedItemId);
        if (item is null)
        {
            return View("Input", new NlCheckoutInputViewModel
            {
                Utterance = utterance,
                ErrorMessage = "Selected item not found. Please try again."
            });
        }

        DateTime? dueDate = DateTime.TryParse(dueDateIso, out var parsed) ? parsed : null;

        var vm = BuildConfirmViewModel(item, assigneeId, assigneeName ?? string.Empty,
            dueDate, dueDateDisplay, utterance);
        return View("Confirm", vm);
    }

    // ── Confirmation ──────────────────────────────────────────────────────────

    /// <summary>
    /// GET /mobile/checkout/nl/confirm
    /// Shows the confirmation screen.
    /// AC2: All extracted entities displayed before any transaction is recorded.
    /// </summary>
    [HttpGet("confirm")]
    public IActionResult Confirm(
        [FromQuery] int itemId,
        [FromQuery] int assigneeId,
        [FromQuery] string? assigneeName,
        [FromQuery] string? dueDateIso,
        [FromQuery] string? dueDateDisplay,
        [FromQuery] string? utterance)
    {
        var item = _equipmentService.GetItem(itemId);
        if (item is null) return NotFound();

        DateTime? dueDate = DateTime.TryParse(dueDateIso, out var parsed) ? parsed : null;

        var vm = BuildConfirmViewModel(item, assigneeId, assigneeName ?? string.Empty,
            dueDate, dueDateDisplay, utterance ?? string.Empty);
        return View("Confirm", vm);
    }

    /// <summary>
    /// POST /mobile/checkout/nl/confirm
    /// Commits the transaction after explicit user confirmation.
    /// AC2: Transaction recorded only after explicit user "Confirm" action.
    /// AC7: Identical data recorded as the standard checkout flow.
    /// </summary>
    [HttpPost("confirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPost(
        [FromForm] int itemId,
        [FromForm] int assigneeId,
        [FromForm] string assigneeName,
        [FromForm] string? dueDateIso,
        [FromForm] string? dueDateDisplay,
        [FromForm] string? utterance)
    {
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var currentUserName = User.FindFirstValue(ClaimTypes.Name) ?? "unknown";

        // Resolve assignee
        int effectiveUserId = assigneeId > 0 ? assigneeId : currentUserId;
        string effectiveName = !string.IsNullOrWhiteSpace(assigneeName) ? assigneeName : currentUserName;

        var item = _equipmentService.GetItem(itemId);
        if (item is null)
            return NotFound();

        // Server-side idempotency (same pattern as standard checkout)
        if (!item.IsAvailable && _equipmentService.IsIdempotentCheckout(itemId, effectiveUserId))
        {
            TempData["SuccessMessage"] = $"'{item.Name}' is already checked out to you (duplicate request detected).";
            return RedirectToAction("Success", "MobileCheckout", new { itemId });
        }

        // Availability conflict
        if (!item.IsAvailable)
        {
            var activeRecord = _equipmentService.GetActiveCheckoutRecord(itemId);
            DateTime? dueDate = DateTime.TryParse(dueDateIso, out var p) ? p : null;
            var vm = BuildConfirmViewModel(item, assigneeId, assigneeName,
                dueDate, dueDateDisplay, utterance ?? string.Empty);
            vm.ErrorMessage = activeRecord is not null
                ? $"Cannot check out: '{item.Name}' is currently held by {activeRecord.BorrowerName}."
                : "Item is unavailable.";
            Response.StatusCode = 409;
            return View("Confirm", vm);
        }

        // Perform checkout — records identical data model as standard flow (AC7)
        var conditionNote = $"NL checkout: \"{utterance}\"".Trim();
        var success = _equipmentService.Checkout(itemId, effectiveName, effectiveUserId, conditionNote);
        if (!success)
        {
            DateTime? dueDate2 = DateTime.TryParse(dueDateIso, out var p2) ? p2 : null;
            var vm = BuildConfirmViewModel(item, assigneeId, assigneeName,
                dueDate2, dueDateDisplay, utterance ?? string.Empty);
            vm.ErrorMessage = "Checkout failed. Tap to retry or use standard checkout.";
            Response.StatusCode = 409;
            return View("Confirm", vm);
        }

        // Coordinator notifications + push (same pattern as standard flow)
        var checkoutRecord = _equipmentService.GetActiveCheckoutRecord(itemId);
        if (checkoutRecord is not null)
        {
            var coordinators = _userService.GetCoordinators();
            var message = $"{effectiveName} checked out '{item.Name}' via NL checkout at {DateTime.UtcNow:g} UTC.";

            foreach (var coordinator in coordinators)
            {
                _notificationService.CreateNotification(coordinator.Id, checkoutRecord.Id, message);

                if (coordinator.NotificationsEnabled && coordinator.PushEndpoint is not null)
                {
                    try
                    {
                        await _pushService.SendAsync(coordinator, "New Equipment Checkout", message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Push notification failed for coordinator {CoordinatorId}. Continuing.",
                            coordinator.Id);
                    }
                }
            }

            _approvalService.CreateRequest(checkoutRecord.Id, effectiveUserId);
        }

        TempData["SuccessMessage"] = $"'{item.Name}' successfully checked out to {effectiveName}. Awaiting coordinator approval.";
        return RedirectToAction("Pending", "MobileCheckout", new { checkoutRecordId = checkoutRecord?.Id ?? 0 });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IActionResult RedirectToNlConfirm(NlParseResult result)
    {
        return RedirectToAction("Confirm", new
        {
            itemId = result.ResolvedItem!.Id,
            assigneeId = result.AssigneeId,
            assigneeName = result.AssigneeName,
            dueDateIso = result.DueDate?.ToString("O"),
            dueDateDisplay = result.DueDateDisplay,
            utterance = result.OriginalUtterance
        });
    }

    private IActionResult ShowDisambiguationView(NlParseResult result, string utterance)
    {
        return View("Disambiguate", new NlCheckoutDisambiguateViewModel
        {
            Utterance = utterance,
            Candidates = result.AmbiguousMatches,
            DueDate = result.DueDate,
            DueDateDisplay = result.DueDateDisplay,
            AssigneeId = result.AssigneeId,
            AssigneeName = result.AssigneeName
        });
    }

    private static NlCheckoutConfirmViewModel BuildConfirmViewModel(
        EquipmentItem item,
        int assigneeId,
        string assigneeName,
        DateTime? dueDate,
        string? dueDateDisplay,
        string utterance)
    {
        return new NlCheckoutConfirmViewModel
        {
            Item = item,
            AssigneeId = assigneeId,
            AssigneeName = assigneeName,
            DueDate = dueDate,
            DueDateDisplay = dueDateDisplay,
            OriginalUtterance = utterance
        };
    }
}
