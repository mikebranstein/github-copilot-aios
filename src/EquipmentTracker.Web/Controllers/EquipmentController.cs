using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EquipmentTracker.Web.Controllers;

public class EquipmentController : Controller
{
    private readonly IEquipmentService _equipmentService;
    private readonly IApprovalService _approvalService;
    private readonly IAccountSettingsService _accountSettingsService;
    private readonly IUserService _userService;
    private readonly IConfiguration _configuration;
    private readonly IConditionAssessmentService? _conditionSvc;

    public EquipmentController(
        IEquipmentService equipmentService,
        IApprovalService approvalService,
        IAccountSettingsService accountSettingsService,
        IUserService userService,
        IConfiguration configuration)
    {
        _equipmentService = equipmentService;
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
            IsRestricted = item.IsRestricted,
            RequiredApprovalType = item.RequiredApprovalType
        };
        PopulateCheckoutSiteFields(model, item);
        return View(model);
    }

    // POST /Equipment/Checkout
    // Extended for Issue #117: restricted equipment triggers approval workflow (AC-2, AC-3)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(CheckoutViewModel model)
    {
        var item = _equipmentService.GetItem(model.EquipmentItemId);
        if (item is null)
            return NotFound();

        var item = _equipmentService.GetItem(model.EquipmentItemId);
        if (item is null)
            return NotFound();

        var borrowerUserId = User.Identity?.IsAuthenticated == true
            ? (int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? (int?)uid : null)
            : null;

        var success = _equipmentService.Checkout(model.EquipmentItemId, model.BorrowerName, borrowerUserId);
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

        // -- AC-2: Restricted equipment - place checkout in Pending Approval state --------
        if (item.IsRestricted && checkoutRecord is not null)
        {
            checkoutRecord.IsPendingApproval = true;

            // Resolve approver: first coordinator found; delegate from settings
            var settings = _accountSettingsService.GetSettings();
            var approvers = _userService.GetApprovers();
            var approverUserId = approvers.FirstOrDefault()?.Id;

            // AC-3: Send push notification to approver (within 2-minute SLA)
            await _approvalService.CreateRestrictedRequestAsync(
                checkoutRecordId: checkoutRecord.Id,
                requestingUserId: borrowerUserId ?? 0,
                equipmentItemId: item.Id,
                approverUserId: approverUserId,
                delegateApproverId: settings.DelegateApproverId,
                equipmentName: item.Name,
                requestorName: model.BorrowerName,
                checkoutDuration: model.CheckoutDuration);

            TempData["SuccessMessage"] =
                $"Checkout request for '{model.EquipmentItemName}' submitted and pending supervisor approval. " +
                "The item is reserved. You will be notified when the decision is made.";
            TempData["PendingApproval"] = "true";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = $"'{model.EquipmentItemName}' checked out to {model.BorrowerName}.";
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

    private void PopulateCheckoutSiteFields(CheckoutViewModel model, EquipmentTracker.Web.Models.EquipmentItem item)
    {
        model.CurrentSiteName = item.SiteId.HasValue ? _siteService.GetSite(item.SiteId.Value)?.Name : null;
        model.SiteOptions =
        [
            new SelectListItem("No change", ""),
            .. _siteService.GetActiveSites().Select(site => new SelectListItem(site.Name, site.Id.ToString()))
        ];
    }
}