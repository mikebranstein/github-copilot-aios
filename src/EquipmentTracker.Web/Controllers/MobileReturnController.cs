using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Handles the mobile return flow: QR scan -> confirm -> done.
/// Routes: /mobile/return/*
/// </summary>
[Authorize]
[Route("mobile/return")]
public class MobileReturnController : Controller
{
    private readonly IEquipmentService _equipmentService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MobileReturnController> _logger;
    private readonly IConditionAssessmentService? _conditionSvc;

    public MobileReturnController(
        IEquipmentService equipmentService,
        IConfiguration configuration,
        ILogger<MobileReturnController> logger,
        IConditionAssessmentService? conditionSvc = null)
    {
        _equipmentService = equipmentService;
        _configuration = configuration;
        _logger = logger;
        _conditionSvc = conditionSvc;
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

        var conditionCaptureRequired = _configuration.GetValue<bool>("Features:ConditionCaptureRequired", false);

        var vm = new MobileReturnConfirmViewModel
        {
            Item = item,
            ActiveRecord = activeRecord,
            ErrorMessage = error,
            ConditionCaptureRequired = conditionCaptureRequired,

            // Fair Witness fields (AC-FW1, AC-FW2) — populated from the checkout record
            FairWitnessPhotoUrl = activeRecord?.ConditionPhotoAtCheckout,
            FairWitnessTimestamp = activeRecord?.CheckedOutAtUtc,
            FairWitnessItemName = item.Name,

            // Return photo button always enabled before return confirmed (AC-R1)
            IsCaptureReturnPhotoButtonEnabled = true,

            // Issue #115 — photo capture enabled by default
            PhotoCaptureEnabled = true
        };

        return View(vm);
    }

    // POST /mobile/return/confirm
    [HttpPost("confirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPost(
        [FromForm] int itemId,
        [FromForm] string? returnConditionNote,
        [FromForm] string? conditionGrade = null)
    {
        var item = _equipmentService.GetItem(itemId);
        if (item is null) return NotFound();

        var conditionCaptureRequired = _configuration.GetValue<bool>("Features:ConditionCaptureRequired", false);

        // Ownership guard: only the borrower or a coordinator may return an item.
        var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";
        var currentUserId = int.TryParse(currentUserIdStr, out var uid) ? uid : 0;
        var isCoordinator = bool.TryParse(User.FindFirstValue("IsCoordinator"), out var cv) && cv;
        var activeRecord = _equipmentService.GetActiveCheckoutRecord(itemId);

        if (activeRecord?.BorrowerUserId != null &&
            activeRecord.BorrowerUserId != currentUserId &&
            !isCoordinator)
        {
            TempData["ErrorMessage"] = "You can only return equipment that you checked out.";
            return RedirectToAction("Scan");
        }

        // AC1: Mandatory condition grade gate — block return if no grade selected
        if (string.IsNullOrWhiteSpace(conditionGrade))
        {
            var vm = new MobileReturnConfirmViewModel
            {
                Item = item,
                ActiveRecord = activeRecord,
                ErrorMessage = "A condition grade is required to complete the return.",
                ConditionCaptureRequired = conditionCaptureRequired,
                PhotoCaptureEnabled = true
            };
            Response.StatusCode = 422;
            return View("Confirm", vm);
        }

        // Parse grade (case-insensitive, default to Good if unknown)
        var grade = Enum.TryParse<ConditionGrade>(conditionGrade, ignoreCase: true, out var g) ? g : ConditionGrade.Good;

        // Validate condition note if required
        if (conditionCaptureRequired && string.IsNullOrWhiteSpace(returnConditionNote))
        {
            var vm = new MobileReturnConfirmViewModel
            {
                Item = item,
                ActiveRecord = activeRecord,
                ErrorMessage = "A condition note is required when returning equipment.",
                ConditionCaptureRequired = true,
                SelectedConditionGrade = conditionGrade
            };
            Response.StatusCode = 422;
            return View("Confirm", vm);
        }

        // Enforce max length
        if (returnConditionNote?.Length > 500)
            returnConditionNote = returnConditionNote[..500];

        if (item.IsAvailable)
        {
            var vm = new MobileReturnConfirmViewModel
            {
                Item = item,
                ErrorMessage = $"'{item.Name}' is already available.",
                ConditionCaptureRequired = conditionCaptureRequired
            };
            Response.StatusCode = 409;
            return View("Confirm", vm);
        }

        var success = _equipmentService.Return(itemId, returnConditionNote);
        if (!success)
        {
            var vm = new MobileReturnConfirmViewModel
            {
                Item = item,
                ErrorMessage = "Return failed. Tap to retry.",
                ConditionCaptureRequired = conditionCaptureRequired
            };
            Response.StatusCode = 409;
            return View("Confirm", vm);
        }

        // AC8: Create immutable condition record (server-timestamped, permanent audit trail)
        int? conditionRecordId = null;
        if (_conditionSvc is not null && activeRecord is not null)
        {
            var operatorName = User.FindFirstValue(ClaimTypes.Name) ?? "unknown";
            var record = _conditionSvc.CreateConditionRecord(
                checkoutRecordId: activeRecord.Id,
                equipmentItemId: itemId,
                equipmentName: item.Name,
                operatorUserId: currentUserId > 0 ? currentUserId : null,
                operatorName: operatorName,
                grade: grade);
            conditionRecordId = record.Id;

            // AC3: Auto-create maintenance ticket draft for Damaged returns
            if (grade == ConditionGrade.Damaged)
            {
                _conditionSvc.CreateMaintenanceTicketDraft(
                    conditionRecordId: record.Id,
                    equipmentItemId: itemId,
                    equipmentName: item.Name,
                    reportedByUserId: currentUserId > 0 ? currentUserId : null,
                    reportedByName: operatorName,
                    reportedAtUtc: record.ServerTimestampUtc,
                    photoIds: new List<int>());
                _logger.LogInformation(
                    "Maintenance ticket draft created for item {ItemId} returned as Damaged (ConditionRecord {RecordId}).",
                    itemId, record.Id);
            }

            // AC4: Flag Lost equipment
            if (grade == ConditionGrade.Lost)
            {
                _conditionSvc.FlagEquipmentLost(
                    equipmentItemId: itemId,
                    conditionRecordId: record.Id,
                    flaggedByUserId: currentUserId > 0 ? currentUserId : null,
                    flaggedByName: operatorName);
                _logger.LogWarning(
                    "Equipment item {ItemId} flagged as LOST by user {User} (ConditionRecord {RecordId}).",
                    itemId, operatorName, record.Id);
            }

            // AC6: Detect scheduling conflicts for Damaged or Lost returns
            if (grade == ConditionGrade.Damaged || grade == ConditionGrade.Lost)
            {
                var alertedTo = grade == ConditionGrade.Lost
                    ? "ops-director@company.com"
                    : "maintenance-coordinator@company.com";
                var alert = _conditionSvc.DetectAndCreateConflictAlert(
                    conditionRecordId: record.Id,
                    equipmentItemId: itemId,
                    alertedTo: alertedTo);
                if (alert is not null && alert.ConflictingReservationIds.Count > 0)
                {
                    TempData["ConflictAlertMessage"] =
                        $"Scheduling conflict detected: {alert.ConflictingReservationIds.Count} upcoming reservation(s) affected.";
                    _logger.LogWarning(
                        "Scheduling conflict for item {ItemId}: {Count} reservations affected.",
                        itemId, alert.ConflictingReservationIds.Count);
                }
            }

            // AC5/AC7: Severity-based notification routing
            if (grade == ConditionGrade.Good || grade == ConditionGrade.Worn)
            {
                // Good/Worn: audit-only, no notification email
                _logger.LogInformation(
                    "Item {ItemId} returned as {Grade} — audit record only, no notification sent.",
                    itemId, grade);
            }
            else if (grade == ConditionGrade.Damaged)
            {
                // Damaged: notify maintenance coordinator
                _logger.LogInformation(
                    "NOTIFY maintenance-coordinator@company.com — item {ItemId} returned Damaged (ConditionRecord {RecordId}).",
                    itemId, record.Id);
            }
            else if (grade == ConditionGrade.Lost)
            {
                // Lost: notify ops director + finance
                _logger.LogWarning(
                    "NOTIFY ops-director@company.com, finance@company.com — item {ItemId} reported Lost (ConditionRecord {RecordId}).",
                    itemId, record.Id);
            }
        }

        // Advance waitlist queue unless item is Lost (AC4: Lost items removed from circulation)
        if (grade != ConditionGrade.Lost)
        {
            try
            {
                var waitlistService = HttpContext.RequestServices.GetService<IWaitlistService>();
                if (waitlistService is not null)
                    await waitlistService.AdvanceQueueAsync(itemId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to advance waitlist queue for item {ItemId} after return.", itemId);
            }
        }

        _logger.LogInformation("Item {ItemId} returned as {Grade} by user {User}.",
            itemId, grade, User.FindFirstValue(ClaimTypes.Name));

        TempData["SuccessMessage"] = $"'{item.Name}' has been returned successfully.";
        TempData["ReturnedConditionGrade"] = grade.ToString();
        if (conditionRecordId.HasValue)
            TempData["ConditionRecordId"] = conditionRecordId.Value.ToString();
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
        ViewBag.ReturnedConditionGrade = TempData["ReturnedConditionGrade"] as string;
        ViewBag.ConditionRecordId = TempData["ConditionRecordId"] as string;
        ViewBag.ConflictAlertMessage = TempData["ConflictAlertMessage"] as string;
        return View();
    }

    // ── Photo capture actions (Issue #58) ─────────────────────────────────────

    // POST /mobile/return/save-return-photo
    [HttpPost("save-return-photo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveReturnPhoto(
        [FromForm] int checkoutRecordId,
        [FromForm] string? photoData)
    {
        var storage = HttpContext.RequestServices.GetService<IPhotoStorageService>();

        if (storage is null)
            return StatusCode(503, new { error = "Photo storage service unavailable." });

        byte[] photoBytes;
        if (!string.IsNullOrWhiteSpace(photoData))
        {
            var base64 = photoData.Contains(',') ? photoData.Split(',')[1] : photoData;
            try { photoBytes = Convert.FromBase64String(base64); }
            catch { photoBytes = Array.Empty<byte>(); }
        }
        else
        {
            photoBytes = Array.Empty<byte>();
        }

        var uploaderName = User.FindFirstValue(ClaimTypes.Name) ?? "unknown";
        var photoUrl = await storage.SavePhotoAsync(photoBytes, uploaderName);

        await storage.AttachToCheckoutRecordAsync(checkoutRecordId, photoUrl, isReturn: true);

        return Json(new { success = true, photoUrl });
    }
}