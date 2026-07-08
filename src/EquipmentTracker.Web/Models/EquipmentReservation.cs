namespace EquipmentTracker.Web.Models;

/// <summary>
/// Represents a future planned checkout / reservation for an equipment item.
/// Used by scheduling conflict detection on Damaged/Lost returns (AC6).
/// Added for Issue #115 — Equipment Condition Assessment &amp; Damage Tracking at Return.
/// </summary>
public class EquipmentReservation
{
    public int Id { get; set; }

    public int EquipmentItemId { get; set; }

    /// <summary>Display name of the operator who made the reservation.</summary>
    public string OperatorName { get; set; } = string.Empty;

    /// <summary>Site/job name for the reservation.</summary>
    public string SiteName { get; set; } = string.Empty;

    public DateTime ReservationStartUtc { get; set; }

    public DateTime? ReservationEndUtc { get; set; }

    public bool IsCancelled { get; set; } = false;
}
