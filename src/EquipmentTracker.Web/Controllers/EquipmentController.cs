using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Web.Controllers;

public class EquipmentController : Controller
{
    private readonly IEquipmentService _equipmentService;
    private readonly IConfiguration _configuration;

    public EquipmentController(IEquipmentService equipmentService, IConfiguration configuration)
    {
        _equipmentService = equipmentService;
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
        if (!ModelState.IsValid)
            return View(model);

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

        TempData["SuccessMessage"] = $"'{model.EquipmentItemName}' checked out to {model.BorrowerName}.";
        return RedirectToAction(nameof(Index));
    }

    // GET /Equipment/History/5
    public IActionResult History(int id)
    {
        var item = _equipmentService.GetItem(id);
        if (item is null)
            return NotFound();

        var history = _equipmentService.GetCheckoutHistory(id);
        var model = new CheckoutHistoryViewModel
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
