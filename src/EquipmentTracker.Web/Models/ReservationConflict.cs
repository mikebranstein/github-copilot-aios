namespace EquipmentTracker.Web.Models;

/// <summary>
/// Describes a detected scheduling conflict and the available alternatives.
/// Added for Issue #123 - Project-Based Equipment Reservation & Scheduling Calendar.
/// </summary>
public class ReservationConflict
{
    /// <summary>The existing reservation that overlaps with the requested range.</summary>
    public Reservation ConflictingReservation { get; set; } = null!;

    /// <summary>Best-effort list of alternative suggestions (may be empty).</summary>
    public List<ReservationAlternative> Alternatives { get; set; } = new();
}
