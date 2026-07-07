namespace EquipmentTracker.Web.Models;

/// <summary>
/// Lifecycle state of a Reservation.
/// Added for Issue #123 - Project-Based Equipment Reservation & Scheduling Calendar.
/// </summary>
public enum ReservationStatus
{
    Active,
    Cancelled,
    Overridden
}
