using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Serves the offline queue management UI (AC5).
/// </summary>
[Authorize]
public class OfflineQueueController : Controller
{
    /// <summary>
    /// GET /OfflineQueue — displays the pending/synced/conflict transaction list (AC5).
    /// The view reads data from IndexedDB client-side; no server-side model is needed.
    /// </summary>
    public IActionResult Index()
    {
        return View();
    }
}
