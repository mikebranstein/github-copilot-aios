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
    private readonly IUserService _userService;

    public OfflineSyncController(
        IOfflineSyncService syncService,
        IEquipmentService equipmentService,
        IUserService userService)
    {
        _syncService = syncService;
        _equipmentService = equipmentService;
        _userService = userService;
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

    /// <summary>
    /// AC5: Lightweight server probe endpoint.
    /// The client sends a HEAD request to this endpoint to verify the server is
    /// reachable before starting the 30-second sync clock.
    /// Returns 200 OK with no body. No authentication required so unauthenticated
    /// captive portals always fail (they return 200 for a different domain).
    /// </summary>
    [AllowAnonymous]
    [HttpHead("probe")]
    [HttpGet("probe")]
    public IActionResult Probe()
    {
        return Ok();
    }

    /// <summary>
    /// AC7: Coordinator override — force-apply a conflicted transaction.
    /// Only coordinators may call this endpoint.
    /// Creates an immutable audit log entry.
    /// </summary>
    [HttpPost("conflict/{deviceTransactionId}/override")]
    public IActionResult OverrideConflict(
        string deviceTransactionId,
        [FromBody] ConflictOverrideRequest request)
    {
        if (string.IsNullOrWhiteSpace(deviceTransactionId))
            return BadRequest(new { error = "deviceTransactionId is required." });

        int userId = GetCurrentUserId();
        if (userId <= 0)
            return Unauthorized();

        var user = _userService.GetById(userId);
        if (user is null || !user.IsCoordinator)
            return Forbid();

        var result = _syncService.CoordinatorOverride(
            deviceTransactionId,
            userId,
            request?.OverrideReason ?? "(no reason given)");

        if (result is null)
            return NotFound(new { error = $"Transaction {deviceTransactionId} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Returns all equipment items as a JSON snapshot for offline catalog caching.
    /// AC2: Service Worker caches this response. Includes a generatedAt field so the
    /// client can show freshness, and exposes flagged status/description for local UX.
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

/// <summary>Request body for coordinator conflict override.</summary>
public class ConflictOverrideRequest
{
    public string? OverrideReason { get; set; }
}
