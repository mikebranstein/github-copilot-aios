using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Web.Controllers;

public class EquipmentController : Controller
{
    private readonly IEquipmentService _equipmentService;
    private readonly ICertificationService _certService;
    private readonly IConfiguration _configuration;

    public EquipmentController(
        IEquipmentService equipmentService,
        ICertificationService certService,
        IConfiguration configuration)
    {
        _equipmentService = equipmentService;
        _certService = certService;
        _configuration = configuration;
    }

    // GET /Equipment
    public IActionResult Index()
    {
        int overdueThresholdDays = _configuration.GetValue<int>("Checkout:OverdueThresholdDays", 7);
        var utcNow = DateTime.UtcNow;

        var items = _equipmentService.GetAllItems();

        var rows = items.Select(item =>
        {
            var activeRecord = item.IsAvailable
                ? null
                : _equipmentService.GetActiveCheckoutRecord(item.Id);

            int daysCheckedOut = 0;
            bool isOverdue = false;

            if (activeRecord is not null)
            {
                daysCheckedOut = (int)(utcNow - activeRecord.CheckedOutAtUtc).TotalDays;
                isOverdue = daysCheckedOut >= overdueThresholdDays;
            }

            return new EquipmentListItemViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Category = item.Category,
                IsAvailable = item.IsAvailable,
                IsOverdue = isOverdue,
                BorrowerName = activeRecord?.BorrowerName,
                DaysCheckedOut = daysCheckedOut
            };
        }).ToList();

        var model = new EquipmentListViewModel
        {
            Items = rows,
            AvailableCount = rows.Count(r => r.IsAvailable)
        };
        return View(model);
    }

    // GET /Equipment/Create
    public IActionResult Create()
    {
        return View(new CreateEquipmentViewModel());
    }

    // POST /Equipment/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(CreateEquipmentViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        _equipmentService.CreateItem(model.Name, model.Category);

        TempData["SuccessMessage"] = $"'{model.Name}' was added successfully.";
        return RedirectToAction(nameof(Index));
    }

    // GET /Equipment/Checkout/5
    public IActionResult Checkout(int id)
    {
        var item = _equipmentService.GetItem(id);
        if (item is null)
            return NotFound();

        if (!item.IsAvailable)
        {
            TempData["ErrorMessage"] = $"'{item.Name}' is not available for checkout.";
            return RedirectToAction(nameof(Index));
        }

        var model = new CheckoutViewModel
        {
            EquipmentItemId = item.Id,
            EquipmentItemName = item.Name
        };
        return View(model);
    }

    // POST /Equipment/Checkout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Checkout(CheckoutViewModel model)
    {
        var item = _equipmentService.GetItem(model.EquipmentItemId);
        if (item is null)
            return NotFound();

        if (!ModelState.IsValid)
        {
            // Preserve cert block message if already set
            if (model.CertBlockMessage is null)
            {
                var outcome = _certService.ValidateCheckout(model.BorrowerName, item.Category);
                if (outcome == CertValidationOutcome.Blocked)
                    model.CertBlockMessage = _certService.GetBlockReasonMessage(model.BorrowerName, item.Category);
            }
            return View(model);
        }

        // ── Cert enforcement gate (AC1) ───────────────────────────────────────
        var certOutcome = CertValidationOutcome.NotRequired;

        if (!model.IsOverrideAttempt)
        {
            certOutcome = _certService.ValidateCheckout(model.BorrowerName, item.Category);
            if (certOutcome == CertValidationOutcome.Blocked)
            {
                model.CertBlockMessage = _certService.GetBlockReasonMessage(model.BorrowerName, item.Category);
                return View(model);
            }
        }
        else
        {
            // Override path (AC2) — supervisor name is mandatory
            if (string.IsNullOrWhiteSpace(model.OverrideSupervisorName))
            {
                ModelState.AddModelError(nameof(model.OverrideSupervisorName),
                    "Supervisor name is required to override a blocked checkout.");
                model.CertBlockMessage = _certService.GetBlockReasonMessage(model.BorrowerName, item.Category);
                return View(model);
            }
            certOutcome = CertValidationOutcome.Overridden;
        }

        // ── Perform checkout ──────────────────────────────────────────────────
        var success = _equipmentService.Checkout(model.EquipmentItemId, model.BorrowerName);
        if (!success)
        {
            var currentHolder = _equipmentService.GetCurrentHolder(model.EquipmentItemId);
            var errorMessage = currentHolder is not null
                ? $"'{model.EquipmentItemName}' is already checked out by {currentHolder}."
                : $"'{model.EquipmentItemName}' is no longer available for checkout.";
            ModelState.AddModelError(string.Empty, errorMessage);
            return View(model);
        }

        // ── Record cert validation result on the checkout record ──────────────
        var checkoutRecord = _equipmentService.GetActiveCheckoutRecord(model.EquipmentItemId);
        if (checkoutRecord is not null)
        {
            checkoutRecord.CertValidationResult = certOutcome;

            if (certOutcome == CertValidationOutcome.Overridden)
            {
                // Determine which cert was required for the block message
                var reqs = _certService.GetRequirementsForCategory(item.Category);
                var firstCertType = reqs.Select(r => _certService.GetCertType(r.CertTypeId)?.Name)
                                        .FirstOrDefault() ?? "certification";

                var overrideRecord = _certService.RecordOverride(
                    checkoutRecord.Id,
                    model.OverrideSupervisorName!,
                    model.OverrideReasonCode,
                    model.OverrideReasonText ?? string.Empty,
                    model.BorrowerName,
                    firstCertType);

                checkoutRecord.OverrideRecordId = overrideRecord.Id;
            }
        }

        TempData["SuccessMessage"] = certOutcome == CertValidationOutcome.Overridden
            ? $"'{model.EquipmentItemName}' checked out to {model.BorrowerName} with supervisor override by {model.OverrideSupervisorName}."
            : $"'{model.EquipmentItemName}' checked out to {model.BorrowerName}.";

        return RedirectToAction(nameof(Index));
    }

    // GET /Equipment/History/5
    public IActionResult History(int id)
    {
        var item = _equipmentService.GetItem(id);
        if (item is null)
            return NotFound();

        var history = _equipmentService.GetCheckoutHistory(id);
        var model = new ItemCheckoutHistoryViewModel
        {
            EquipmentItemId = item.Id,
            EquipmentItemName = item.Name,
            History = history
        };
        return View(model);
    }

    // POST /Equipment/Return/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Return(int id)
    {
        var item = _equipmentService.GetItem(id);
        if (item is null)
            return NotFound();

        var success = _equipmentService.Return(id);
        if (!success)
        {
            TempData["ErrorMessage"] = $"'{item.Name}' is already available and cannot be returned again.";
        }
        else
        {
            TempData["SuccessMessage"] = $"'{item.Name}' has been returned successfully.";
        }

        return RedirectToAction(nameof(Index));
    }
}
