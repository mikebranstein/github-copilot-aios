using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-memory implementation of soft hold management.
/// Uses a lock to enforce first-write-wins atomicity, simulating a DB-level unique constraint
/// on (equipment_id) WHERE released_at IS NULL AND expires_at > NOW().
/// Added for Issue #118 — Real-Time Equipment Availability Dashboard.
/// </summary>
public class SoftHoldService : ISoftHoldService
{
    private readonly List<SoftHold> _holds = new();
    private readonly object _lock = new();
    private int _nextId = 1;
    private const int HoldDurationMinutes = 30;

    public Task<SoftHold?> PlaceHoldAsync(int equipmentItemId, int userId)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            // First-write-wins: reject if an active hold already exists
            var existing = _holds.FirstOrDefault(h =>
                h.EquipmentItemId == equipmentItemId && h.IsActive(now));

            if (existing is not null)
                return Task.FromResult<SoftHold?>(null);

            var hold = new SoftHold
            {
                Id = _nextId++,
                EquipmentItemId = equipmentItemId,
                UserId = userId,
                HeldAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(HoldDurationMinutes)
            };
            _holds.Add(hold);
            return Task.FromResult<SoftHold?>(hold);
        }
    }

    public Task<bool> ReleaseHoldAsync(int holdId, int userId)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var hold = _holds.FirstOrDefault(h => h.Id == holdId && h.IsActive(now));

            if (hold is null || hold.UserId != userId)
                return Task.FromResult(false);

            hold.ReleasedAtUtc = now;
            return Task.FromResult(true);
        }
    }

    public SoftHold? GetActiveHold(int equipmentItemId)
    {
        var now = DateTime.UtcNow;
        return _holds.FirstOrDefault(h => h.EquipmentItemId == equipmentItemId && h.IsActive(now));
    }

    public IReadOnlyList<SoftHold> GetHoldsForItem(int equipmentItemId) =>
        _holds.Where(h => h.EquipmentItemId == equipmentItemId)
              .OrderByDescending(h => h.HeldAtUtc)
              .ToList()
              .AsReadOnly();

    public Task<IReadOnlyList<int>> ExpireStaleHoldsAsync()
    {
        var now = DateTime.UtcNow;
        // Active holds whose expiry time has passed naturally expire — no mutation needed;
        // IsActive() already returns false once ExpiresAtUtc <= now.
        // Return the item IDs of holds that just crossed the expiry boundary so Notify Me can fire.
        var expiredItemIds = _holds
            .Where(h => h.ReleasedAtUtc is null && h.ExpiresAtUtc <= now)
            .Select(h => h.EquipmentItemId)
            .Distinct()
            .ToList();

        IReadOnlyList<int> result = expiredItemIds;
        return Task.FromResult(result);
    }
}
