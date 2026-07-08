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

    // ── GET /api/offline/catalog-snapshot ────────────────────────────────────

    /// <summary>
    /// Returns all equipment items as a JSON snapshot for offline catalog caching.
    /// AC2: Service Worker caches this response. Includes a "generatedAt" field so
    /// the client can show "As of [timestamp]" and warn if cache is >24 h old.
    /// Issue #121: IsFlagged and FlagDescription included so mobile client can reflect
    /// damage flag status in the local cache before the next sync.
    /// </summary>
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
                Status = i.IsFlagged
                    ? EquipmentTracker.Web.ViewModels.EquipmentStatus.Flagged
                    : (i.IsAvailable
                        ? EquipmentTracker.Web.ViewModels.EquipmentStatus.Available
                        : EquipmentTracker.Web.ViewModels.EquipmentStatus.CheckedOut),
                FlagDescription = i.IsFlagged ? i.FlagDescription : null
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
