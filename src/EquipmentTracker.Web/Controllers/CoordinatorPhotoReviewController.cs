using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Coordinator workflow: review side-by-side checkout vs return photos and set damage assessment.
/// Routes: /coordinator/photo-review/*
/// Added for Issue #58 — Photo-Backed Checkout &amp; Return.
/// </summary>
[Authorize]
[Route("coordinator/photo-review")]
public class CoordinatorPhotoReviewController : Controller
{
    private readonly IEquipmentService _equipmentService;
    private readonly IUserService _userService;
    private readonly ILogger<CoordinatorPhotoReviewController> _logger;

    public CoordinatorPhotoReviewController(
        IEquipmentService equipmentService,
        IUserService userService,
        ILogger<CoordinatorPhotoReviewController> logger)
    {
        _equipmentService = equipmentService;
        _userService = userService;
        _logger = logger;
    }

    // GET /coordinator/photo-review
    [HttpGet("")]
    public IActionResult Index()
    {
        var returned = _equipmentService.GetAllRawCheckoutRecords()
            .Where(r => r.ReturnedAtUtc is not null)
            .OrderByDescending(r => r.ReturnedAtUtc)
            .ToList();

        var itemNameById = _equipmentService.GetAllItems().ToDictionary(i => i.Id, i => i.Name);

        var viewModels = returned.Select(r => new CoordinatorPhotoReviewViewModel
        {
            CheckoutRecordId = r.Id,
            BorrowerName = r.BorrowerName,
            ItemName = itemNameById.TryGetValue(r.EquipmentItemId, out var name) ? name : "(unknown)",
            CheckedOutAtUtc = r.CheckedOutAtUtc,
            ReturnedAtUtc = r.ReturnedAtUtc,
            CheckoutPhotoUrl = r.ConditionPhotoAtCheckout,
            ReturnPhotoUrl = r.ConditionPhotoAtReturn,
            ConditionAssessment = r.ConditionAssessment ?? "NoDamage"
        }).ToList();

        return View(viewModels);
    }

    // GET /coordinator/photo-review/review/{checkoutRecordId}
    [HttpGet("review/{checkoutRecordId:int}")]
    public IActionResult Review(int checkoutRecordId)
    {
        var record = _equipmentService.GetCheckoutRecordById(checkoutRecordId);
        if (record is null) return NotFound();

        var itemNameById = _equipmentService.GetAllItems().ToDictionary(i => i.Id, i => i.Name);

        var vm = new CoordinatorPhotoReviewViewModel
        {
            CheckoutRecordId = record.Id,
            BorrowerName = record.BorrowerName,
            ItemName = itemNameById.TryGetValue(record.EquipmentItemId, out var name) ? name : "(unknown)",
            CheckedOutAtUtc = record.CheckedOutAtUtc,
            ReturnedAtUtc = record.ReturnedAtUtc,
            CheckoutPhotoUrl = record.ConditionPhotoAtCheckout,
            ReturnPhotoUrl = record.ConditionPhotoAtReturn,
            ConditionAssessment = record.ConditionAssessment ?? "NoDamage"
        };

        return View(vm);
    }

    // POST /coordinator/photo-review/set-damage-assessment
    [HttpPost("set-damage-assessment")]
    [ValidateAntiForgeryToken]
    public IActionResult SetDamageAssessment(
        [FromForm] int checkoutRecordId,
        [FromForm] string assessment)
    {
        // Validate assessment value
        var valid = new[] { "NoDamage", "MinorDamage", "SignificantDamage" };
        if (!valid.Contains(assessment))
            return BadRequest(new { error = $"Invalid assessment value '{assessment}'." });

        var record = _equipmentService.GetCheckoutRecordById(checkoutRecordId);
        if (record is null) return NotFound();

        record.ConditionAssessment = assessment;

        _logger.LogInformation(
            "Coordinator set damage assessment for record {RecordId}: {Assessment}",
            checkoutRecordId, assessment);

        TempData["SuccessMessage"] = $"Damage assessment saved: {assessment}";
        return RedirectToAction("Review", new { checkoutRecordId });
    }
}
