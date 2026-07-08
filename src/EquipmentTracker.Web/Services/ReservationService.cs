using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-memory implementation of IReservationService.
/// Provides atomic conflict detection (lock-based), alternative suggestion engine,
/// cross-site aggregation, and displacement notification routing.
/// Added for Issue #123 - Project-Based Equipment Reservation & Scheduling Calendar.
/// </summary>
public class ReservationService : IReservationService
{
    private readonly IEquipmentService _equipmentService;
    private readonly IReservationNotificationService _notificationService;

    private readonly List<Project> _projects = new();
    private readonly List<Reservation> _reservations = new();
    private readonly object _lock = new();

    private int _nextProjectId = 1;
    private int _nextReservationId = 1;

    public ReservationService(
        IEquipmentService equipmentService,
        IReservationNotificationService notificationService)
    {
        _equipmentService = equipmentService;
        _notificationService = notificationService;

        // Seed sample sites on the equipment service items
        SeedSiteData();
    }

    // ── Seeding ─────────────────────────────────────────────────────────────

    private void SeedSiteData()
    {
        var items = _equipmentService.GetAllItems();
        int siteIndex = 1;
        foreach (var item in items)
        {
            // Assign items to 3 rotating sites for demo/test purposes
            item.SiteId = (siteIndex % 3) + 1;
            item.SiteName = $"Site {item.SiteId}";
            siteIndex++;
        }
    }

    // ── Project management ──────────────────────────────────────────────────

    public Project CreateProject(
        string name, DateOnly startDate, DateOnly endDate,
        int siteId, string siteName, int ownerId, string ownerName)
    {
        lock (_lock)
        {
            var project = new Project
            {
                Id = _nextProjectId++,
                Name = name,
                StartDate = startDate,
                EndDate = endDate,
                SiteId = siteId,
                SiteName = siteName,
                OwnerId = ownerId,
                OwnerName = ownerName,
                CreatedAt = DateTime.UtcNow
            };
            _projects.Add(project);
            return project;
        }
    }

    public IReadOnlyList<Project> GetAllProjects()
    {
        lock (_lock) { return _projects.ToList(); }
    }

    public Project? GetProject(int projectId)
    {
        lock (_lock) { return _projects.FirstOrDefault(p => p.Id == projectId); }
    }

    // ── Reservation CRUD ────────────────────────────────────────────────────

    public (Reservation? Created, IReadOnlyList<ReservationConflict> Conflicts) TryCreateReservation(
        int projectId, int equipmentId, DateOnly startDate, DateOnly endDate,
        int createdByUserId, string createdByUserName)
    {
        lock (_lock)
        {
            var conflicts = DetectConflictsInternal(equipmentId, startDate, endDate, excludeReservationId: null);
            if (conflicts.Count > 0)
                return (null, conflicts);

            var reservation = BuildReservation(projectId, equipmentId, startDate, endDate, createdByUserId, createdByUserName);
            _reservations.Add(reservation);

            // Confirmation notification (best-effort, outside lock safe)
            Task.Run(() => _notificationService.NotifyCreated(reservation, createdByUserId));

            return (reservation, Array.Empty<ReservationConflict>());
        }
    }

    public Reservation CreateReservationWithOverride(
        int projectId, int equipmentId, DateOnly startDate, DateOnly endDate,
        int createdByUserId, string createdByUserName, int overridingUserId)
    {
        lock (_lock)
        {
            // Displace any conflicting active reservations
            var conflicts = DetectConflictsInternal(equipmentId, startDate, endDate, excludeReservationId: null);
            foreach (var conflict in conflicts)
            {
                var displaced = conflict.ConflictingReservation;
                displaced.Status = ReservationStatus.Overridden;
                displaced.OverriddenByUserId = overridingUserId;
                displaced.CancelledAt = DateTime.UtcNow;

                // Notify displaced party outside lock
                var capturedDisplaced = displaced;
                Task.Run(() => _notificationService.NotifyDisplaced(
                    capturedDisplaced,
                    GetProject(projectId)?.Name ?? "Unknown Project",
                    "/Reservation"));
            }

            var reservation = BuildReservation(projectId, equipmentId, startDate, endDate, createdByUserId, createdByUserName);
            reservation.OverriddenByUserId = overridingUserId;
            _reservations.Add(reservation);

            Task.Run(() => _notificationService.NotifyCreated(reservation, createdByUserId));

            return reservation;
        }
    }

    public (bool Updated, IReadOnlyList<ReservationConflict> Conflicts) TryEditReservation(
        int reservationId, DateOnly newStartDate, DateOnly newEndDate, int newEquipmentId,
        int requestingUserId)
    {
        // Prevent moving start date into the past
        if (newStartDate < DateOnly.FromDateTime(DateTime.Today))
            return (false, Array.Empty<ReservationConflict>());

        lock (_lock)
        {
            var existing = _reservations.FirstOrDefault(r => r.Id == reservationId && r.Status == ReservationStatus.Active);
            if (existing == null) return (false, Array.Empty<ReservationConflict>());

            var conflicts = DetectConflictsInternal(newEquipmentId, newStartDate, newEndDate, excludeReservationId: reservationId);
            if (conflicts.Count > 0) return (false, conflicts);

            existing.StartDate = newStartDate;
            existing.EndDate = newEndDate;
            existing.EquipmentId = newEquipmentId;

            var item = _equipmentService.GetItem(newEquipmentId);
            if (item != null)
            {
                existing.EquipmentName = item.Name;
                existing.EquipmentCategory = item.Category;
                existing.SiteId = item.SiteId ?? 0;
                existing.SiteName = item.SiteName;
            }

            return (true, Array.Empty<ReservationConflict>());
        }
    }

    public bool CancelReservation(int reservationId, int requestingUserId, bool isOperationsManager)
    {
        lock (_lock)
        {
            var reservation = _reservations.FirstOrDefault(r => r.Id == reservationId && r.Status == ReservationStatus.Active);
            if (reservation == null) return false;

            bool canCancel = reservation.CreatedByUserId == requestingUserId || isOperationsManager;
            if (!canCancel) return false;

            bool cancelledByOther = reservation.CreatedByUserId != requestingUserId && isOperationsManager;

            reservation.Status = ReservationStatus.Cancelled;
            reservation.CancelledAt = DateTime.UtcNow;

            if (cancelledByOther)
            {
                var capturedReservation = reservation;
                Task.Run(() => _notificationService.NotifyCancelled(capturedReservation, capturedReservation.CreatedByUserId));
            }

            return true;
        }
    }

    public Reservation? GetReservation(int reservationId)
    {
        lock (_lock) { return _reservations.FirstOrDefault(r => r.Id == reservationId); }
    }

    public IReadOnlyList<Reservation> GetReservationsForProject(int projectId)
    {
        lock (_lock) { return _reservations.Where(r => r.ProjectId == projectId).ToList(); }
    }

    public IReadOnlyList<Reservation> GetActiveReservationsForEquipment(int equipmentId)
    {
        lock (_lock)
        {
            return _reservations
                .Where(r => r.EquipmentId == equipmentId && r.Status == ReservationStatus.Active)
                .ToList();
        }
    }

    public IReadOnlyList<Reservation> GetAllProjectReservationsForUser(int userId)
    {
        lock (_lock)
        {
            return _reservations
                .Where(r => r.CreatedByUserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
        }
    }

    // ── Calendar view ───────────────────────────────────────────────────────

    public IReadOnlyList<Reservation> GetCalendarReservations(
        DateOnly from, DateOnly to, int? siteId = null, int? projectId = null)
    {
        lock (_lock)
        {
            return _reservations
                .Where(r => r.Status == ReservationStatus.Active
                    && r.StartDate <= to
                    && r.EndDate >= from
                    && (siteId == null || r.SiteId == siteId)
                    && (projectId == null || r.ProjectId == projectId))
                .ToList();
        }
    }

    // ── Cross-site availability ─────────────────────────────────────────────

    public IReadOnlyList<EquipmentAvailabilitySummary> GetCrossSiteAvailability(
        DateOnly from, DateOnly to, int? siteId = null)
    {
        var allItems = _equipmentService.GetAllItems()
            .Where(i => siteId == null || i.SiteId == siteId)
            .ToList();

        int totalDays = to.DayNumber - from.DayNumber + 1;
        var summaries = new List<EquipmentAvailabilitySummary>();

        lock (_lock)
        {
            foreach (var item in allItems)
            {
                // Count how many days in the window are covered by active reservations
                var bookedDays = new HashSet<int>(); // day numbers

                var overlappingReservations = _reservations
                    .Where(r => r.EquipmentId == item.Id
                        && r.Status == ReservationStatus.Active
                        && r.StartDate <= to
                        && r.EndDate >= from);

                foreach (var reservation in overlappingReservations)
                {
                    var start = reservation.StartDate < from ? from : reservation.StartDate;
                    var end = reservation.EndDate > to ? to : reservation.EndDate;
                    for (var d = start; d <= end; d = d.AddDays(1))
                        bookedDays.Add(d.DayNumber);
                }

                int bookedCount = bookedDays.Count;
                AvailabilityStatus status =
                    bookedCount == 0 ? AvailabilityStatus.Available :
                    bookedCount >= totalDays ? AvailabilityStatus.FullyBooked :
                    AvailabilityStatus.PartiallyBooked;

                summaries.Add(new EquipmentAvailabilitySummary
                {
                    EquipmentId = item.Id,
                    EquipmentName = item.Name,
                    Category = item.Category,
                    SiteId = item.SiteId ?? 0,
                    SiteName = item.SiteName,
                    Status = status,
                    BookedDayCount = bookedCount,
                    TotalDayCount = totalDays
                });
            }
        }

        return summaries.OrderBy(s => s.SiteName).ThenBy(s => s.EquipmentName).ToList();
    }

    // ── Conflict detection ──────────────────────────────────────────────────

    public IReadOnlyList<ReservationConflict> DetectConflicts(
        int equipmentId, DateOnly startDate, DateOnly endDate, int? excludeReservationId = null)
    {
        lock (_lock)
        {
            return DetectConflictsInternal(equipmentId, startDate, endDate, excludeReservationId);
        }
    }

    private List<ReservationConflict> DetectConflictsInternal(
        int equipmentId, DateOnly startDate, DateOnly endDate, int? excludeReservationId)
    {
        // Must be called under _lock
        var overlapping = _reservations
            .Where(r => r.EquipmentId == equipmentId
                && r.Status == ReservationStatus.Active
                && r.StartDate <= endDate
                && r.EndDate >= startDate
                && (excludeReservationId == null || r.Id != excludeReservationId))
            .ToList();

        if (overlapping.Count == 0) return new List<ReservationConflict>();

        var conflicts = new List<ReservationConflict>();

        foreach (var conflicting in overlapping)
        {
            var alternatives = BuildAlternatives(equipmentId, startDate, endDate, excludeReservationId);
            conflicts.Add(new ReservationConflict
            {
                ConflictingReservation = conflicting,
                Alternatives = alternatives
            });
        }

        return conflicts;
    }

    private List<ReservationAlternative> BuildAlternatives(
        int requestedEquipmentId, DateOnly startDate, DateOnly endDate, int? excludeReservationId)
    {
        // Must be called under _lock
        var alternatives = new List<ReservationAlternative>();
        var requestedItem = _equipmentService.GetItem(requestedEquipmentId);
        if (requestedItem == null) return alternatives;

        // (a) Same-category substitute asset available for the requested dates
        var substitutes = _equipmentService.GetAllItems()
            .Where(i => i.Id != requestedEquipmentId
                && i.Category == requestedItem.Category
                && !_reservations.Any(r => r.EquipmentId == i.Id
                    && r.Status == ReservationStatus.Active
                    && r.StartDate <= endDate
                    && r.EndDate >= startDate
                    && (excludeReservationId == null || r.Id != excludeReservationId)))
            .Take(2)
            .ToList();

        foreach (var sub in substitutes)
        {
            alternatives.Add(new ReservationAlternative
            {
                Type = AlternativeType.SubstituteAsset,
                Description = $"{sub.Name} ({sub.Category}) is available for your requested dates.",
                SuggestedEquipmentId = sub.Id,
                SuggestedEquipmentName = sub.Name
            });
        }

        // (b) Adjusted date range for the requested asset — find next available gap
        var nextStart = FindNextAvailableDate(requestedEquipmentId, endDate.AddDays(1), startDate, endDate, excludeReservationId);
        if (nextStart.HasValue)
        {
            var duration = endDate.DayNumber - startDate.DayNumber;
            var nextEnd = nextStart.Value.AddDays(duration);
            alternatives.Add(new ReservationAlternative
            {
                Type = AlternativeType.AdjustedDateRange,
                Description = $"{requestedItem.Name} is available from {nextStart:MMM d} to {nextEnd:MMM d}.",
                SuggestedStartDate = nextStart,
                SuggestedEndDate = nextEnd
            });
        }

        // (c) Same category at a different site
        if (requestedItem.SiteId > 0)
        {
            var diffSite = _equipmentService.GetAllItems()
                .Where(i => i.Id != requestedEquipmentId
                    && i.SiteId != requestedItem.SiteId
                    && i.Category == requestedItem.Category
                    && !_reservations.Any(r => r.EquipmentId == i.Id
                        && r.Status == ReservationStatus.Active
                        && r.StartDate <= endDate
                        && r.EndDate >= startDate
                        && (excludeReservationId == null || r.Id != excludeReservationId)))
                .FirstOrDefault();

            if (diffSite != null)
            {
                alternatives.Add(new ReservationAlternative
                {
                    Type = AlternativeType.DifferentSite,
                    Description = $"{diffSite.Name} ({diffSite.Category}) is available at {diffSite.SiteName} for your requested dates.",
                    SuggestedEquipmentId = diffSite.Id,
                    SuggestedEquipmentName = diffSite.Name,
                    SuggestedSiteName = diffSite.SiteName
                });
            }
        }

        return alternatives;
    }

    private DateOnly? FindNextAvailableDate(
        int equipmentId, DateOnly searchFrom, DateOnly originalStart, DateOnly originalEnd,
        int? excludeReservationId)
    {
        // Must be called under _lock. Cap search at 90 days forward from originalStart.
        int duration = originalEnd.DayNumber - originalStart.DayNumber;
        var maxSearch = originalStart.AddDays(90);

        var bookedRanges = _reservations
            .Where(r => r.EquipmentId == equipmentId
                && r.Status == ReservationStatus.Active
                && r.EndDate >= searchFrom
                && (excludeReservationId == null || r.Id != excludeReservationId))
            .OrderBy(r => r.StartDate)
            .ToList();

        var candidate = searchFrom;
        while (candidate <= maxSearch)
        {
            var candidateEnd = candidate.AddDays(duration);
            bool overlaps = bookedRanges.Any(r => r.StartDate <= candidateEnd && r.EndDate >= candidate);
            if (!overlaps) return candidate;
            // Jump to the day after the earliest overlapping reservation ends
            var blocker = bookedRanges.First(r => r.StartDate <= candidateEnd && r.EndDate >= candidate);
            candidate = blocker.EndDate.AddDays(1);
        }

        return null;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private Reservation BuildReservation(
        int projectId, int equipmentId, DateOnly startDate, DateOnly endDate,
        int createdByUserId, string createdByUserName)
    {
        var project = _projects.FirstOrDefault(p => p.Id == projectId);
        var item = _equipmentService.GetItem(equipmentId);

        return new Reservation
        {
            Id = _nextReservationId++,
            ProjectId = projectId,
            ProjectName = project?.Name ?? string.Empty,
            EquipmentId = equipmentId,
            EquipmentName = item?.Name ?? string.Empty,
            EquipmentCategory = item?.Category ?? string.Empty,
            SiteId = item?.SiteId ?? 0,
            SiteName = item?.SiteName ?? string.Empty,
            StartDate = startDate,
            EndDate = endDate,
            CreatedByUserId = createdByUserId,
            CreatedByUserName = createdByUserName,
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }
}
