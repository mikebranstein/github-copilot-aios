using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Web.Controllers;

[Authorize]
public class MobileEquipmentController : Controller
{
    private readonly IEquipmentService _equipmentService;
    private readonly IConfiguration _configuration;

    public MobileEquipmentController(IEquipmentService equipmentService, IConfiguration configuration)
    {
        _equipmentService = equipmentService;
        _configuration = configuration;
    }

    private int OverdueThresholdDays
    {
        get
        {
            var days = _configuration.GetValue<int>("Checkout:OverdueThresholdDays", 7);
            return days <= 0 ? 7 : days;
        }
    }

    [HttpGet]
    public IActionResult Index()
    {
        var items = _equipmentService.GetAllItems();
        var viewModels = items.Select(item => BuildCatalogItem(item.Id)).ToList();
        return View(viewModels);
    }

    [HttpGet]
    public IActionResult Detail(int id)
    {
        var item = _equipmentService.GetItem(id);
        if (item is null) return NotFound();

        var record = _equipmentService.GetActiveCheckoutRecord(id);
        var thresholdDays = OverdueThresholdDays;

        var vm = new EquipmentDetailViewModel
        {
            Id = item.Id,
            Name = item.Name,
            Category = item.Category,
            BorrowerName = record?.BorrowerName,
            CheckedOutAtUtc = record?.CheckedOutAtUtc,
            DueDateUtc = record is not null ? record.CheckedOutAtUtc.AddDays(thresholdDays) : null,
            Status = GetStatus(item.Id)
        };

        return View(vm);
    }

    private CatalogItemViewModel BuildCatalogItem(int itemId)
    {
        var item = _equipmentService.GetItem(itemId)!;
        return new CatalogItemViewModel
        {
            Id = item.Id,
            Name = item.Name,
            Category = item.Category,
            Status = GetStatus(itemId)
        };
    }

    private EquipmentStatus GetStatus(int itemId)
    {
        var item = _equipmentService.GetItem(itemId);
        if (item is null || item.IsAvailable) return EquipmentStatus.Available;

        var record = _equipmentService.GetActiveCheckoutRecord(itemId);
        if (record is null) return EquipmentStatus.Available;

        var age = DateTime.UtcNow - record.CheckedOutAtUtc;
        return age.TotalDays >= OverdueThresholdDays ? EquipmentStatus.Overdue : EquipmentStatus.CheckedOut;
    }
}
