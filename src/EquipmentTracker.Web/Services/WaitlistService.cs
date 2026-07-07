using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public class WaitlistService : IWaitlistService
{
    private static readonly WaitlistStatus[] ExitedStatuses =
    [
        WaitlistStatus.Cancelled,
        WaitlistStatus.Fulfilled,
        WaitlistStatus.Forfeited,
        WaitlistStatus.CoordinatorRemoved
    ];

    private readonly List<WaitlistEntry> _entries = new();
    private readonly List<QueueAuditEvent> _auditEvents = new();
    private readonly IEquipmentService _equipmentService;
    private readonly IPushNotificationService _pushService;
    private readonly IUserService _userService;
    private readonly ILogger<WaitlistService> _logger;
    private readonly object _lock = new();
    private int _nextEntryId = 1;
    private int _nextAuditId = 1;

    public WaitlistService(
        IEquipmentService equipmentService,
        IPushNotificationService pushService,
        IUserService userService,
        ILogger<WaitlistService> logger)
    {
        _equipmentService = equipmentService;
        _pushService = pushService;
        _userService = userService;
        _logger = logger;
    }

    public Task<WaitlistEntry> JoinQueueAsync(int equipmentItemId, int userId, string userName, WaitlistTier tier = WaitlistTier.Standard)
    {
        var item = _equipmentService.GetItem(equipmentItemId)
            ?? throw new ArgumentException($"Equipment item {equipmentItemId} was not found.", nameof(equipmentItemId));

        WaitlistEntry entry;

        lock (_lock)
        {
            entry = new WaitlistEntry
            {
                Id = _nextEntryId++,
                EquipmentItemId = equipmentItemId,
                BorrowerUserId = userId,
                BorrowerName = userName,
                Tier = tier,
                Status = WaitlistStatus.Waiting,
                JoinedAtUtc = DateTime.UtcNow
            };

            _entries.Add(entry);
            RecalculatePositionsLocked(equipmentItemId);
            AddAuditEventLocked(equipmentItemId, entry.Id, userName, "Join", $"{userName} joined the queue for {item.Name} ({tier}).");
        }

        _logger.LogInformation("User {UserId} joined waitlist for item {ItemId}.", userId, equipmentItemId);
        return Task.FromResult(CloneEntry(entry));
    }

    public async Task<bool> CancelEntryAsync(int entryId, int requestingUserId)
    {
        int? advanceItemId = null;
        bool success = false;

        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == entryId);
            if (entry is null || entry.BorrowerUserId != requestingUserId || IsExited(entry.Status))
            {
                success = false;
            }
            else
            {
                advanceItemId = entry.Status == WaitlistStatus.Reserved ? entry.EquipmentItemId : null;

                entry.Status = WaitlistStatus.Cancelled;
                entry.ExitedAtUtc = DateTime.UtcNow;
                entry.ExitReason = "Cancelled by borrower";
                entry.ConfirmationDeadlineUtc = null;
                RecalculatePositionsLocked(entry.EquipmentItemId);
                AddAuditEventLocked(entry.EquipmentItemId, entry.Id, entry.BorrowerName, "Cancel", $"{entry.BorrowerName} cancelled their waitlist entry.");
                success = true;
            }
        }

        if (advanceItemId.HasValue)
        {
            await AdvanceQueueAsync(advanceItemId.Value);
        }

        return success;
    }

    public Task<bool> ConfirmReservationAsync(int entryId, int userId)
    {
        bool success = false;

        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == entryId);
            if (entry is null || entry.BorrowerUserId != userId || entry.Status != WaitlistStatus.Reserved)
            {
                return Task.FromResult(false);
            }

            entry.Status = WaitlistStatus.Fulfilled;
            entry.ExitedAtUtc = DateTime.UtcNow;
            entry.ExitReason = "Reservation confirmed";
            entry.ConfirmationDeadlineUtc = null;
            AddAuditEventLocked(entry.EquipmentItemId, entry.Id, entry.BorrowerName, "Confirm", $"{entry.BorrowerName} confirmed their reservation.");
            success = true;
        }

        return Task.FromResult(success);
    }

    public async Task AdvanceQueueAsync(int equipmentItemId)
    {
        ApplicationUser? targetUser = null;
        string? notificationTitle = null;
        string? notificationBody = null;
        string? borrowerName = null;

        var item = _equipmentService.GetItem(equipmentItemId);
        if (item is null)
        {
            return;
        }

        lock (_lock)
        {
            if (_entries.Any(e => e.EquipmentItemId == equipmentItemId && e.Status == WaitlistStatus.Reserved))
            {
                return;
            }

            RecalculatePositionsLocked(equipmentItemId);

            var nextEntry = _entries
                .Where(e => e.EquipmentItemId == equipmentItemId && e.Status == WaitlistStatus.Waiting)
                .OrderBy(e => e.QueuePosition)
                .ThenBy(e => e.JoinedAtUtc)
                .FirstOrDefault();

            if (nextEntry is null)
            {
                return;
            }

            var now = DateTime.UtcNow;
            nextEntry.Status = WaitlistStatus.Reserved;
            nextEntry.ReservedAtUtc = now;
            nextEntry.ConfirmationDeadlineUtc = now.AddMinutes(15);
            borrowerName = nextEntry.BorrowerName;

            AddAuditEventLocked(
                equipmentItemId,
                nextEntry.Id,
                "System",
                "Advance",
                $"Reserved {item.Name} for {nextEntry.BorrowerName} until {nextEntry.ConfirmationDeadlineUtc:yyyy-MM-dd HH:mm} UTC.");

            targetUser = _userService.GetById(nextEntry.BorrowerUserId);
            notificationTitle = $"Equipment reserved: {item.Name}";
            notificationBody = $"Your waitlist entry for '{item.Name}' is ready. Confirm by {nextEntry.ConfirmationDeadlineUtc:HH:mm} UTC at /Waitlist/Confirm?entryId={nextEntry.Id}.";
            RecalculatePositionsLocked(equipmentItemId);
        }

        if (targetUser is not null && targetUser.NotificationsEnabled && notificationTitle is not null && notificationBody is not null)
        {
            await _pushService.SendAsync(targetUser, notificationTitle, notificationBody);
        }

        _logger.LogInformation("Advanced waitlist queue for item {ItemId} to borrower {BorrowerName}.", equipmentItemId, borrowerName);
    }

    public async Task ExpireTimedOutReservationsAsync()
    {
        List<int> affectedEquipmentItemIds;

        lock (_lock)
        {
            var now = DateTime.UtcNow;
            affectedEquipmentItemIds = _entries
                .Where(e =>
                    e.Status == WaitlistStatus.Reserved &&
                    e.ConfirmationDeadlineUtc.HasValue &&
                    e.ConfirmationDeadlineUtc.Value <= now)
                .Select(e => e.EquipmentItemId)
                .Distinct()
                .ToList();

            foreach (var entry in _entries.Where(e =>
                         e.Status == WaitlistStatus.Reserved &&
                         e.ConfirmationDeadlineUtc.HasValue &&
                         e.ConfirmationDeadlineUtc.Value <= now))
            {
                entry.Status = WaitlistStatus.Forfeited;
                entry.ExitedAtUtc = now;
                entry.ExitReason = "Confirmation deadline expired";
                AddAuditEventLocked(entry.EquipmentItemId, entry.Id, "System", "Forfeit", $"Entry forfeited because confirmation deadline expired for {entry.BorrowerName}.");
            }

            foreach (var equipmentItemId in affectedEquipmentItemIds)
            {
                RecalculatePositionsLocked(equipmentItemId);
            }
        }

        foreach (var equipmentItemId in affectedEquipmentItemIds)
        {
            await AdvanceQueueAsync(equipmentItemId);
        }
    }

    public async Task<bool> OverridePositionAsync(int entryId, int newPosition, string reason, string coordinatorName)
    {
        ApplicationUser? targetUser = null;
        string? notificationTitle = null;
        string? notificationBody = null;
        bool success = false;

        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == entryId);
            if (entry is null || entry.Status != WaitlistStatus.Waiting)
            {
                success = false;
            }
            else
            {
                var waitingEntries = _entries
                    .Where(e => e.EquipmentItemId == entry.EquipmentItemId && e.Status == WaitlistStatus.Waiting)
                    .OrderBy(e => e.QueuePosition)
                    .ThenByDescending(e => e.Tier)
                    .ThenBy(e => e.JoinedAtUtc)
                    .ThenBy(e => e.Id)
                    .ToList();

                var boundedPosition = Math.Clamp(newPosition, 1, waitingEntries.Count);
                waitingEntries.Remove(entry);
                waitingEntries.Insert(boundedPosition - 1, entry);

                ApplyExplicitOrderLocked(waitingEntries);
                entry.OverrideReason = reason;

                AddAuditEventLocked(
                    entry.EquipmentItemId,
                    entry.Id,
                    coordinatorName,
                    "Override",
                    $"{coordinatorName} moved {entry.BorrowerName} to position {boundedPosition}. Reason: {reason}");

                targetUser = _userService.GetById(entry.BorrowerUserId);
                notificationTitle = "Waitlist position updated";
                notificationBody = $"Your queue position for equipment item #{entry.EquipmentItemId} was updated to #{boundedPosition}. Reason: {reason}";
                success = true;
            }
        }

        if (targetUser is not null && targetUser.NotificationsEnabled && notificationTitle is not null && notificationBody is not null)
        {
            await _pushService.SendAsync(targetUser, notificationTitle, notificationBody);
        }

        return success;
    }

    public async Task<bool> RemoveEntryAsync(int entryId, string reason, string coordinatorName)
    {
        int? advanceItemId = null;
        bool success = false;

        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == entryId);
            if (entry is null || IsExited(entry.Status))
            {
                success = false;
            }
            else
            {
                advanceItemId = entry.Status == WaitlistStatus.Reserved ? entry.EquipmentItemId : null;

                entry.Status = WaitlistStatus.CoordinatorRemoved;
                entry.ExitedAtUtc = DateTime.UtcNow;
                entry.ExitReason = reason;
                entry.ConfirmationDeadlineUtc = null;
                AddAuditEventLocked(entry.EquipmentItemId, entry.Id, coordinatorName, "Remove", $"{coordinatorName} removed {entry.BorrowerName}. Reason: {reason}");
                RecalculatePositionsLocked(entry.EquipmentItemId);
                success = true;
            }
        }

        if (advanceItemId.HasValue)
        {
            await AdvanceQueueAsync(advanceItemId.Value);
        }

        return success;
    }

    public Task<bool> MarkUrgentAsync(int entryId, string coordinatorName)
    {
        bool success = false;

        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == entryId);
            if (entry is null || entry.Status != WaitlistStatus.Waiting)
            {
                return Task.FromResult(false);
            }

            entry.Tier = WaitlistTier.Urgent;
            RecalculatePositionsLocked(entry.EquipmentItemId);
            AddAuditEventLocked(entry.EquipmentItemId, entry.Id, coordinatorName, "MarkUrgent", $"{coordinatorName} marked {entry.BorrowerName} as urgent.");
            success = true;
        }

        return Task.FromResult(success);
    }

    public Task<List<WaitlistEntry>> GetQueueForItemAsync(int equipmentItemId)
    {
        lock (_lock)
        {
            var queue = _entries
                .Where(e => e.EquipmentItemId == equipmentItemId && (e.Status == WaitlistStatus.Waiting || e.Status == WaitlistStatus.Reserved))
                .OrderBy(e => e.Status == WaitlistStatus.Reserved ? 0 : 1)
                .ThenBy(e => e.QueuePosition == 0 ? int.MaxValue : e.QueuePosition)
                .ThenBy(e => e.JoinedAtUtc)
                .Select(CloneEntry)
                .ToList();

            return Task.FromResult(queue);
        }
    }

    public Task<WaitlistEntry?> GetEntryAsync(int entryId)
    {
        lock (_lock)
        {
            return Task.FromResult(_entries.FirstOrDefault(e => e.Id == entryId) is { } entry ? CloneEntry(entry) : null);
        }
    }

    public Task<(int Position, string EtaDisplay)> GetPositionAndEtaAsync(int entryId)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == entryId)
                ?? throw new ArgumentException($"Waitlist entry {entryId} was not found.", nameof(entryId));

            var position = entry.Status switch
            {
                WaitlistStatus.Waiting => entry.QueuePosition,
                WaitlistStatus.Reserved => 1,
                _ => Math.Max(entry.QueuePosition, 0)
            };

            var fulfilledEntries = _entries
                .Where(e =>
                    e.EquipmentItemId == entry.EquipmentItemId &&
                    e.Status == WaitlistStatus.Fulfilled &&
                    e.ExitedAtUtc.HasValue)
                .OrderBy(e => e.ExitedAtUtc)
                .ToList();

            if (fulfilledEntries.Count < 3)
            {
                return Task.FromResult((position, "ETA unavailable — not enough history"));
            }

            var averageTicks = fulfilledEntries
                .Average(e => (e.ExitedAtUtc!.Value - e.JoinedAtUtc).Ticks);

            var etaTicks = averageTicks * Math.Max(position, 1);
            var lower = TimeSpan.FromTicks(Math.Max(0L, (long)(etaTicks * 0.8d)));
            var upper = TimeSpan.FromTicks(Math.Max(0L, (long)(etaTicks * 1.2d)));

            return Task.FromResult((position, $"~{FormatDuration(lower)} – {FormatDuration(upper)}"));
        }
    }

    public Task<List<(int EquipmentItemId, List<WaitlistEntry> Queue)>> GetAllActiveQueuesAsync()
    {
        lock (_lock)
        {
            var queues = _entries
                .Where(e => e.Status == WaitlistStatus.Waiting)
                .GroupBy(e => e.EquipmentItemId)
                .OrderBy(g => g.Key)
                .Select(g => (EquipmentItemId: g.Key, Queue: g.OrderBy(e => e.QueuePosition).ThenBy(e => e.JoinedAtUtc).Select(CloneEntry).ToList()))
                .ToList();

            return Task.FromResult(queues);
        }
    }

    public Task<List<WaitlistEntry>> GetHistoryForItemAsync(int equipmentItemId)
    {
        lock (_lock)
        {
            var history = _entries
                .Where(e => e.EquipmentItemId == equipmentItemId && IsExited(e.Status))
                .OrderByDescending(e => e.ExitedAtUtc)
                .Select(CloneEntry)
                .ToList();

            return Task.FromResult(history);
        }
    }

    public Task<List<EquipmentItem>> GetAlternativesForCategoryAsync(int equipmentItemId)
    {
        var sourceItem = _equipmentService.GetItem(equipmentItemId)
            ?? throw new ArgumentException($"Equipment item {equipmentItemId} was not found.", nameof(equipmentItemId));

        int currentQueueLength;
        Dictionary<int, int> queueLengths;

        lock (_lock)
        {
            queueLengths = _entries
                .Where(e => e.Status == WaitlistStatus.Waiting)
                .GroupBy(e => e.EquipmentItemId)
                .ToDictionary(g => g.Key, g => g.Count());

            currentQueueLength = queueLengths.TryGetValue(equipmentItemId, out var count) ? count : 0;
        }

        var alternatives = _equipmentService.GetAllItems()
            .Where(i => i.Id != equipmentItemId && string.Equals(i.Category, sourceItem.Category, StringComparison.OrdinalIgnoreCase))
            .Where(i => !queueLengths.TryGetValue(i.Id, out var length) || length < currentQueueLength)
            .OrderBy(i => queueLengths.TryGetValue(i.Id, out var length) ? length : 0)
            .ThenByDescending(i => i.IsAvailable)
            .ThenBy(i => i.Name)
            .ToList();

        return Task.FromResult(alternatives);
    }

    public IReadOnlyList<QueueAuditEvent> GetAuditLog(int equipmentItemId)
    {
        lock (_lock)
        {
            return _auditEvents
                .Where(a => a.EquipmentItemId == equipmentItemId)
                .OrderByDescending(a => a.OccurredAtUtc)
                .Select(CloneAuditEvent)
                .ToList()
                .AsReadOnly();
        }
    }

    private void RecalculatePositionsLocked(int equipmentItemId)
    {
        var waitingEntries = _entries
            .Where(e => e.EquipmentItemId == equipmentItemId && e.Status == WaitlistStatus.Waiting)
            .OrderByDescending(e => e.Tier)
            .ThenBy(e => e.JoinedAtUtc)
            .ThenBy(e => e.Id)
            .ToList();

        for (var index = 0; index < waitingEntries.Count; index++)
        {
            waitingEntries[index].QueuePosition = index + 1;
        }
    }

    private void ApplyExplicitOrderLocked(List<WaitlistEntry> orderedEntries)
    {
        if (orderedEntries.Count == 0)
        {
            return;
        }

        var baseJoinedAt = orderedEntries.Min(e => e.JoinedAtUtc);
        for (var index = 0; index < orderedEntries.Count; index++)
        {
            orderedEntries[index].QueuePosition = index + 1;
            orderedEntries[index].JoinedAtUtc = baseJoinedAt.AddMilliseconds(index);
        }
    }

    private void AddAuditEventLocked(int equipmentItemId, int? entryId, string actorName, string eventType, string? details)
    {
        _auditEvents.Add(new QueueAuditEvent
        {
            Id = _nextAuditId++,
            EquipmentItemId = equipmentItemId,
            WaitlistEntryId = entryId,
            ActorName = actorName,
            EventType = eventType,
            Details = details,
            OccurredAtUtc = DateTime.UtcNow
        });
    }

    private static bool IsExited(WaitlistStatus status) => ExitedStatuses.Contains(status);

    private static WaitlistEntry CloneEntry(WaitlistEntry entry) => new()
    {
        Id = entry.Id,
        EquipmentItemId = entry.EquipmentItemId,
        BorrowerUserId = entry.BorrowerUserId,
        BorrowerName = entry.BorrowerName,
        QueuePosition = entry.QueuePosition,
        Tier = entry.Tier,
        Status = entry.Status,
        JoinedAtUtc = entry.JoinedAtUtc,
        ReservedAtUtc = entry.ReservedAtUtc,
        ConfirmationDeadlineUtc = entry.ConfirmationDeadlineUtc,
        ExitedAtUtc = entry.ExitedAtUtc,
        ExitReason = entry.ExitReason,
        OverrideReason = entry.OverrideReason
    };

    private static QueueAuditEvent CloneAuditEvent(QueueAuditEvent auditEvent) => new()
    {
        Id = auditEvent.Id,
        EquipmentItemId = auditEvent.EquipmentItemId,
        WaitlistEntryId = auditEvent.WaitlistEntryId,
        ActorName = auditEvent.ActorName,
        EventType = auditEvent.EventType,
        Details = auditEvent.Details,
        OccurredAtUtc = auditEvent.OccurredAtUtc
    };

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
        {
            var days = (int)Math.Floor(duration.TotalDays);
            var hours = duration.Hours;
            return hours > 0 ? $"{days}d {hours}h" : $"{days}d";
        }

        if (duration.TotalHours >= 1)
        {
            var hours = (int)Math.Floor(duration.TotalHours);
            var minutes = duration.Minutes;
            return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
        }

        var mins = Math.Max(0, (int)Math.Round(duration.TotalMinutes));
        return $"{mins}m";
    }
}
