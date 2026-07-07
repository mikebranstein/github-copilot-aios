using System.Security.Claims;
using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Web.Controllers;

[Authorize]
[Route("api/offline")]
[ApiController]
public class OfflineSyncController : Controller
{
    private readonly IOfflineSyncService _syncService;
    private readonly IEquipmentService _equipmentService;

    public OfflineSyncController(IOfflineSyncService syncService, IEquipmentService equipmentService)
    {
        _syncService = syncService;
        _equipmentService = equipmentService;
    }

    [HttpPost("sync")]
    public IActionResult Sync([FromBody] List<OfflineSyncTransaction> transactions)
    {
        if (transactions is null || transactions.Count == 0)
            return BadRequest(new { error = "No transactions provided." });

        int userId = GetCurrentUserId();
        if (userId <= 0)
            return Unauthorized();

        var results = _syncService.ProcessBatch(transactions, userId);
        return Ok(results);
    }

    [HttpGet("sync-status/{deviceTransactionId}")]
    public IActionResult SyncStatus(string deviceTransactionId)
    {
        if (string.IsNullOrWhiteSpace(deviceTransactionId))
            return BadRequest(new { error = "deviceTransactionId is required." });

        var result = _syncService.GetResult(deviceTransactionId);
        if (result is null)
            return NotFound(new { status = "unknown", conflictDetails = (string?)null });

        return Ok(result);
    }

    [HttpGet("catalog-snapshot")]
    public IActionResult CatalogSnapshot()
    {
        var items = _equipmentService.GetAllItems();
        var snapshot = new
        {
            generatedAtUtc = DateTime.UtcNow,
            items = items.Select(i => new CatalogItemViewModel
            {
                Id = i.Id,
                Name = i.Name,
                Category = i.Category,
                Status = i.IsAvailable
                    ? EquipmentTracker.Web.ViewModels.EquipmentStatus.Available
                    : EquipmentTracker.Web.ViewModels.EquipmentStatus.CheckedOut
            }).ToList()
        };

        return Ok(snapshot);
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out int id) ? id : 0;
    }
}
