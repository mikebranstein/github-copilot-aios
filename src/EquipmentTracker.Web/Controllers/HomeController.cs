using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Equipment");
    }
}
