namespace EquipmentTracker.Web.Models;

/// <summary>
/// A 30-minute soft hold placed by a field manager to claim equipment availability.
/// Implements first-write-wins concurrency semantics:
///   - Only one active hold (released_at IS NULL and expires_at > now) is permitted per equipment item.
///   - When a second hold is attempted on the same item, it is rejected immediately with a race-condition message.
/// Added for Issue #118 — Real-Time Equipment Availability Dashboard.
/// </summary>
public class SoftHold
{
    public int Id { get; set; }

    public int EquipmentItemId { get; set; }

    public int UserId { get; set; }

    /// <summary>UTC timestamp when the hold was placed.</summary>
    public DateTime HeldAtUtc { get; set; }

    /// <summary>UTC timestamp when the hold expires (HeldAtUtc + 30 minutes).</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>UTC timestamp when the hold was released early by the user. Null if still active or expired naturally.</summary>
    public DateTime? ReleasedAtUtc { get; set; }

    /// <summary>True when the hold is currently active (not released and not expired).</summary>
    public bool IsActive(DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        return ReleasedAtUtc is null && ExpiresAtUtc > now;
    }

    /// <summary>Remaining hold duration in minutes (0 when expired).</summary>
    public int RemainingMinutes(DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var remaining = (ExpiresAtUtc - now).TotalMinutes;
        return (int)Math.Max(0, Math.Ceiling(remaining));
    }
}
