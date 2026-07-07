using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Reservation service interface for project-based equipment scheduling.
/// Added for Issue #123 - Project-Based Equipment Reservation & Scheduling Calendar.
/// </summary>
public interface IReservationService
{
    // ── Project management ──────────────────────────────────────────────────

    Project CreateProject(string name, DateOnly startDate, DateOnly endDate, int siteId, string siteName, int ownerId, string ownerName);
    IReadOnlyList<Project> GetAllProjects();
    Project? GetProject(int projectId);

    // ── Reservation CRUD ────────────────────────────────────────────────────

    /// <summary>
    /// Attempt to create a reservation. Returns null conflicts list when successful.
    /// Returns a populated conflict list when overlapping reservations are detected (reservation is NOT saved).
    /// </summary>
    (Reservation? Created, IReadOnlyList<ReservationConflict> Conflicts) TryCreateReservation(
        int projectId, int equipmentId, DateOnly startDate, DateOnly endDate,
        int createdByUserId, string createdByUserName);

    /// <summary>
    /// Force-create a reservation, displacing any conflicting active reservations.
    /// Only callable by Operations Manager or Admin roles (enforced by caller via <paramref name="overridingUser"/>).
    /// </summary>
    Reservation CreateReservationWithOverride(
        int projectId, int equipmentId, DateOnly startDate, DateOnly endDate,
        int createdByUserId, string createdByUserName,
        int overridingUserId);

    /// <summary>
    /// Edit the date range or equipment asset of an existing reservation.
    /// Returns null conflicts list when update is applied. Past start dates are rejected.
    /// </summary>
    (bool Updated, IReadOnlyList<ReservationConflict> Conflicts) TryEditReservation(
        int reservationId, DateOnly newStartDate, DateOnly newEndDate, int newEquipmentId,
        int requestingUserId);

    /// <summary>
    /// Cancel a reservation. Owner can cancel their own; Operations Manager can cancel any.
    /// Returns false if the requesting user is not authorised.
    /// </summary>
    bool CancelReservation(int reservationId, int requestingUserId, bool isOperationsManager);

    Reservation? GetReservation(int reservationId);
    IReadOnlyList<Reservation> GetReservationsForProject(int projectId);
    IReadOnlyList<Reservation> GetActiveReservationsForEquipment(int equipmentId);

    /// <summary>Returns all reservations created by a given user (all statuses).</summary>
    IReadOnlyList<Reservation> GetAllProjectReservationsForUser(int userId);

    // ── Calendar view ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns active reservations overlapping the given window, optionally filtered by site/project.
    /// </summary>
    IReadOnlyList<Reservation> GetCalendarReservations(
        DateOnly from, DateOnly to, int? siteId = null, int? projectId = null);

    // ── Cross-site availability ─────────────────────────────────────────────

    /// <summary>
    /// Returns availability status for every equipment asset over the given window.
    /// If <paramref name="siteId"/> is non-null, restricts to that site.
    /// </summary>
    IReadOnlyList<EquipmentAvailabilitySummary> GetCrossSiteAvailability(
        DateOnly from, DateOnly to, int? siteId = null);

    // ── Conflict detection helpers ──────────────────────────────────────────

    /// <summary>
    /// Returns conflicts (and alternatives) for the proposed booking WITHOUT persisting anything.
    /// </summary>
    IReadOnlyList<ReservationConflict> DetectConflicts(
        int equipmentId, DateOnly startDate, DateOnly endDate, int? excludeReservationId = null);
}

/// <summary>Availability roll-up for one equipment asset over a time window.</summary>
public class EquipmentAvailabilitySummary
{
    public int EquipmentId { get; set; }
    public string EquipmentName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int SiteId { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public AvailabilityStatus Status { get; set; }
    /// <summary>Number of days in the window that are fully booked.</summary>
    public int BookedDayCount { get; set; }
    /// <summary>Total days in the window.</summary>
    public int TotalDayCount { get; set; }
}

public enum AvailabilityStatus
{
    Available,
    PartiallyBooked,
    FullyBooked
}
