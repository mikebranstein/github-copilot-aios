using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IConfiguration _configuration;
    private readonly ILogger<MobileReturnController> _logger;

    public MobileReturnController(
        IEquipmentService equipmentService,
        IConfiguration configuration,
        ILogger<MobileReturnController> logger)
    {
        _equipmentService = equipmentService;
        _configuration = configuration;
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
            IsCaptureReturnPhotoButtonEnabled = true
        };

        return View(vm);
    }

    // POST /mobile/return/confirm
    [HttpPost("confirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPost([FromForm] int itemId, [FromForm] string? returnConditionNote)
    {
        var item = _equipmentService.GetItem(itemId);
        if (item is null) return NotFound();

        var conditionCaptureRequired = _configuration.GetValue<bool>("Features:ConditionCaptureRequired", false);

        // Ownership guard: only the borrower or a coordinator may return an item.
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var isCoordinator = bool.TryParse(User.FindFirstValue("IsCoordinator"), out var cv) && cv;
        var activeRecord = _equipmentService.GetActiveCheckoutRecord(itemId);

        if (activeRecord?.BorrowerUserId != null &&
            activeRecord.BorrowerUserId != currentUserId &&
            !isCoordinator)
        {
            TempData["ErrorMessage"] = "You can only return equipment that you checked out.";
            return RedirectToAction("Scan");
        }

        // Validate condition note if required
        if (conditionCaptureRequired && string.IsNullOrWhiteSpace(returnConditionNote))
        {
            var vm = new MobileReturnConfirmViewModel
            {
                Item = item,
                ActiveRecord = activeRecord,
                ErrorMessage = "A condition note is required when returning equipment.",
                ConditionCaptureRequired = true
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
