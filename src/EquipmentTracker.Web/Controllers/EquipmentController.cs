using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Web.Controllers;

public class EquipmentController : Controller
{
    private readonly IEquipmentService _equipmentService;

    public EquipmentController(IEquipmentService equipmentService)
    {
        _equipmentService = equipmentService;
    }

    // GET /Equipment
    public IActionResult Index()
    {
        var items = _equipmentService.GetAllItems();
        var model = new EquipmentListViewModel
        {
            Items = items,
            AvailableCount = items.Count(i => i.IsAvailable)
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
