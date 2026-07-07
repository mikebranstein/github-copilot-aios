using System.Security.Claims;
using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EquipmentTracker.Web.Controllers;

public class EquipmentController : Controller
{
    private readonly IEquipmentService _equipmentService;
    private readonly ISiteService _siteService;
    private readonly ICertificationService _certService;
    private readonly IApprovalService? _approvalService;
    private readonly IAccountSettingsService? _accountSettingsService;
    private readonly IUserService? _userService;
    private readonly IConfiguration _configuration;
    private readonly IConditionAssessmentService? _conditionSvc;

    public EquipmentController(
        IEquipmentService equipmentService,
        ISiteService siteService,
        ICertificationService certService,
        IConfiguration configuration,
        IApprovalService? approvalService = null,
        IAccountSettingsService? accountSettingsService = null,
        IUserService? userService = null,
        IConditionAssessmentService? conditionSvc = null)
    {
        _equipmentService = equipmentService;
        _siteService = siteService;
        _certService = certService;
        _approvalService = approvalService;
        _accountSettingsService = accountSettingsService;
        _userService = userService;
        _configuration = configuration;
        _conditionSvc = conditionSvc;
    }

    public IActionResult Index()
    {
        int overdueThresholdDays = _configuration.GetValue<int>("Checkout:OverdueThresholdDays", 7);
        var utcNow = DateTime.UtcNow;
        var items = _equipmentService.GetAllItems();
        var siteNames = _siteService.GetAllSites().ToDictionary(site => site.Id, site => site.Name);

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
                DaysCheckedOut = daysCheckedOut,
                Status = item.Status,
                SiteName = item.SiteId.HasValue && siteNames.TryGetValue(item.SiteId.Value, out var siteName) ? siteName : null
            };
        }).ToList();

        var model = new EquipmentListViewModel
        {
            Items = rows,
            AvailableCount = rows.Count(r => r.Status == EquipmentTracker.Web.Models.EquipmentStatus.Available)
        };
        return View(model);
    }

    public IActionResult Create()
    {
        return View(new CreateEquipmentViewModel());
    }

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
            EquipmentItemName = item.Name,
            ConfirmedSiteId = item.SiteId,
            IsRestricted = item.IsRestricted,
            RequiredApprovalType = item.RequiredApprovalType
        };
        PopulateCheckoutSiteFields(model, item);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(CheckoutViewModel model)
    {
        var item = _equipmentService.GetItem(model.EquipmentItemId);
        if (item is null)
            return NotFound();

        if (model.ConfirmedSiteId.HasValue)
        {
            var selectedSite = _siteService.GetSite(model.ConfirmedSiteId.Value);
            if (selectedSite is null || !selectedSite.IsActive)
                ModelState.AddModelError(nameof(model.ConfirmedSiteId), "Please choose an active site.");
        }

        if (!ModelState.IsValid)
        {
            if (model.CertBlockMessage is null && !string.IsNullOrWhiteSpace(model.BorrowerName))
            {
                var outcome = _certService.ValidateCheckout(model.BorrowerName, item.Category);
                if (outcome == CertValidationOutcome.Blocked)
                    model.CertBlockMessage = _certService.GetBlockReasonMessage(model.BorrowerName, item.Category);
            }

            PopulateCheckoutSiteFields(model, item);
            return View(model);
        }

        var certOutcome = CertValidationOutcome.NotRequired;

        if (!model.IsOverrideAttempt)
        {
            certOutcome = _certService.ValidateCheckout(model.BorrowerName, item.Category);
            if (certOutcome == CertValidationOutcome.Blocked)
            {
                model.CertBlockMessage = _certService.GetBlockReasonMessage(model.BorrowerName, item.Category);
                PopulateCheckoutSiteFields(model, item);
                return View(model);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(model.OverrideSupervisorName))
            {
                ModelState.AddModelError(nameof(model.OverrideSupervisorName),
                    "Supervisor name is required to override a blocked checkout.");
                model.CertBlockMessage = _certService.GetBlockReasonMessage(model.BorrowerName, item.Category);
                PopulateCheckoutSiteFields(model, item);
                return View(model);
            }

            certOutcome = CertValidationOutcome.Overridden;
        }

        var borrowerUserId = User.Identity?.IsAuthenticated == true
            && int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
            ? uid
            : 0;

        var success = _equipmentService.Checkout(
            model.EquipmentItemId,
            model.BorrowerName,
            borrowerUserId == 0 ? null : borrowerUserId,
            newSiteId: model.ConfirmedSiteId);
        if (!success)
        {
            var currentHolder = _equipmentService.GetCurrentHolder(model.EquipmentItemId);
            var errorMessage = currentHolder is not null
                ? $"'{model.EquipmentItemName}' is already checked out by {currentHolder}."
                : $"'{model.EquipmentItemName}' is no longer available for checkout.";
            ModelState.AddModelError(string.Empty, errorMessage);
            PopulateCheckoutSiteFields(model, item);
            return View(model);
        }

        var checkoutRecord = _equipmentService.GetActiveCheckoutRecord(model.EquipmentItemId);
        if (checkoutRecord is not null)
        {
            checkoutRecord.CertValidationResult = certOutcome;

            if (certOutcome == CertValidationOutcome.Overridden)
            {
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

            if (item.IsRestricted && _approvalService is not null && _accountSettingsService is not null && _userService is not null)
            {
                checkoutRecord.IsPendingApproval = true;

                var settings = _accountSettingsService.GetSettings();
                var approverUserId = _userService.GetApprovers().FirstOrDefault()?.Id;

                await _approvalService.CreateRestrictedRequestAsync(
                    checkoutRecord.Id,
                    borrowerUserId,
                    item.Id,
                    approverUserId,
                    settings.DelegateApproverId,
                    item.Name,
                    model.BorrowerName,
                    model.CheckoutDuration);

                TempData["SuccessMessage"] =
                    $"Checkout request for '{model.EquipmentItemName}' submitted and pending supervisor approval. The item is reserved. You will be notified when the decision is made.";
                TempData["PendingApproval"] = "true";
                return RedirectToAction(nameof(Index));
            }
        }

        TempData["SuccessMessage"] = certOutcome == CertValidationOutcome.Overridden
            ? $"'{model.EquipmentItemName}' checked out to {model.BorrowerName} with supervisor override by {model.OverrideSupervisorName}."
            : $"'{model.EquipmentItemName}' checked out to {model.BorrowerName}.";

        return RedirectToAction(nameof(Index));
    }

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

    public IActionResult ConditionHistory(int id)
    {
        var item = _equipmentService.GetItem(id);
        if (item is null)
            return NotFound();

        var records = _conditionSvc?.GetConditionHistory(id) ?? (IReadOnlyList<ConditionRecord>)Array.Empty<ConditionRecord>();
        var historyEntries = records
            .OrderByDescending(r => r.ServerTimestampUtc)
            .Select(r => new ConditionHistoryEntry
            {
                ConditionRecordId = r.Id,
                CheckoutRecordId = r.CheckoutRecordId,
                Grade = r.Grade,
                OperatorName = r.OperatorName,
                ConditionServerTimestampUtc = r.ServerTimestampUtc,
                SyncStatus = r.SyncStatus,
                Photos = r.Photos,
                HasMaintenanceTicket = r.MaintenanceTicketDraft is not null,
                HasLostFlag = r.LostEquipmentFlag is not null,
                HasConflictAlert = r.SchedulingConflictAlert is not null
            })
            .ToList();

        var model = new ConditionHistoryViewModel
        {
            EquipmentItemId = item.Id,
            EquipmentItemName = item.Name,
            EquipmentCategory = item.Category,
            EquipmentStatus = item.LifecycleStatus,
            History = historyEntries
        };
        return View(model);
    }

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

    private void PopulateCheckoutSiteFields(CheckoutViewModel model, EquipmentItem item)
    {
        model.CurrentSiteName = item.SiteId.HasValue ? _siteService.GetSite(item.SiteId.Value)?.Name : null;
        model.SiteOptions =
        [
            new SelectListItem("No change", ""),
            .. _siteService.GetActiveSites().Select(site => new SelectListItem(site.Name, site.Id.ToString()))
        ];
    }
}
