using System.Security.Claims;
using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Provides offline-sync API endpoints consumed by the service worker and client JS.
/// All endpoints require authentication (cookie auth).
/// </summary>
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

    // ── POST /api/offline/sync ────────────────────────────────────────────────

    /// <summary>
    /// Accepts a batch of offline transactions, processes them chronologically,
    /// and returns per-transaction results.
    /// </summary>
    [HttpPost("sync")]
    public IActionResult Sync([FromBody] List<OfflineSyncTransaction> transactions)
    {
        if (transactions is null || transactions.Count == 0)
            return BadRequest(new { error = "No transactions provided." });

        // Risk 4 (MEDIUM): validate that BorrowerUserId matches requesting user (or coordinator).
        // Actual validation is performed inside OfflineSyncService.ProcessBatch().
        int userId = GetCurrentUserId();
        if (userId <= 0)
            return Unauthorized();

        var results = _syncService.ProcessBatch(transactions, userId);
        return Ok(results);
    }

    // ── GET /api/offline/sync-status/{deviceTransactionId} ───────────────────

    /// <summary>
    /// Risk 5 (LOW): Returns the sync result for a specific device transaction ID.
    /// </summary>
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
                Status = i.IsAvailable ? EquipmentStatus.Available : EquipmentStatus.CheckedOut
            }).ToList()
        };

        return Ok(snapshot);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private int GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out int id) ? id : 0;
    }
}
