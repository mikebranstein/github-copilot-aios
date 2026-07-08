using System.Security.Claims;
using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Standalone damage flag API for field workers.
/// Added for Issue #121 — Offline-First Mobile App for Field Workers.
///
/// AC2: The Damage Flag entry point must be accessible directly from the home screen —
/// NOT nested inside a checkout flow. Workers discover damage independently of the
/// checkout context (retrieving equipment from storage, not during active checkout).
///
/// OSHA compliance: Dual timestamps (device_timestamp + server_received_at) are
/// stored for every damage flag per OSHA 1926.95(a), 1926.20(b)(2), 1926.1412(d)(1).
/// </summary>
[Authorize]
[Route("api/damage-flags")]
[ApiController]
public class DamageFlagController : Controller
{
    private readonly IEquipmentService _equipmentService;

    public DamageFlagController(IEquipmentService equipmentService)
    {
        _equipmentService = equipmentService;
    }

    // ── GET /api/damage-flags ─────────────────────────────────────────────────

    /// <summary>
    /// Returns all active damage flags. Used by coordinators to review outstanding flags.
    /// </summary>
    [HttpGet]
    public IActionResult GetAll()
    {
        var flags = _equipmentService.GetAllActiveDamageFlags();
        return Ok(flags);
    }

    // ── GET /api/damage-flags/{itemId} ────────────────────────────────────────

    /// <summary>
    /// Returns all damage flags for the given equipment item.
    /// </summary>
    [HttpGet("{itemId:int}")]
    public IActionResult GetForItem(int itemId)
    {
        var item = _equipmentService.GetItem(itemId);
        if (item is null)
            return NotFound(new { error = $"Equipment item {itemId} not found." });

        var flags = _equipmentService.GetDamageFlags(itemId);
        return Ok(flags);
    }

    // ── POST /api/damage-flags ────────────────────────────────────────────────

    /// <summary>
    /// Submits a standalone damage flag directly (online path).
    /// For offline-sync path, use POST /api/offline/sync with type="damage_flag".
    ///
    /// AC2: This is the standalone entry point — not nested inside a checkout.
    /// Photos are out of scope for offline MVP (Phase 2).
    /// </summary>
    [HttpPost]
    public IActionResult Submit([FromBody] DamageFlagRequest request)
    {
        if (request is null || request.ItemId <= 0)
            return BadRequest(new { error = "ItemId is required." });

        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { error = "Description is required." });

        int userId = GetCurrentUserId();
        if (userId <= 0)
            return Unauthorized();

        string deviceTransactionId = request.DeviceTransactionId ?? Guid.NewGuid().ToString();
        DateTime deviceTimestamp = request.DeviceTimestamp ?? DateTime.UtcNow;

        var flag = _equipmentService.FlagDamage(
            request.ItemId,
            request.Description,
            userId,
            deviceTransactionId,
            deviceTimestamp);

        if (flag is null)
            return NotFound(new { error = $"Equipment item {request.ItemId} not found." });

        return Ok(new
        {
            message = "Damage flag submitted successfully.",
            flag = flag,
            // Plain-language confirmation (AC2)
            confirmation = $"Damage reported for item {request.ItemId}. Your report has been recorded."
        });
    }

    // ── DELETE /api/damage-flags/{itemId} (coordinator only) ──────────────────

    /// <summary>
    /// Clears the damage flag on the given item (coordinator action after resolution).
    /// </summary>
    [HttpDelete("{itemId:int}")]
    public IActionResult ClearFlag(int itemId)
    {
        bool cleared = _equipmentService.ClearDamageFlag(itemId);
        if (!cleared)
            return NotFound(new { error = $"No active damage flag found for item {itemId}." });

        return Ok(new { message = $"Damage flag cleared for item {itemId}." });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private int GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out int id) ? id : 0;
    }
}

/// <summary>
/// Request body for POST /api/damage-flags.
/// </summary>
public class DamageFlagRequest
{
    public int ItemId { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Client-generated UUID. Used for idempotency when the online POST retries.
    /// If not provided, the server generates one.
    /// </summary>
    public string? DeviceTransactionId { get; set; }

    /// <summary>
    /// UTC timestamp when the flag was created on the device.
    /// If not provided, defaults to server time (online submission).
    /// </summary>
    public DateTime? DeviceTimestamp { get; set; }
}
