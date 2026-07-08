namespace EquipmentTracker.Web.Models;

/// <summary>
/// Alert raised when a Damaged or Lost return conflicts with a future reservation/checkout.
/// Surfaced to the operations manager / dispatcher for action.
/// Added for Issue #115 — Equipment Condition Assessment &amp; Damage Tracking at Return.
/// </summary>
public class SchedulingConflictAlert
{
    public int Id { get; set; }

    /// <summary>FK to the ConditionRecord that triggered this alert.</summary>
    public int ConditionRecordId { get; set; }

    /// <summary>FK to EquipmentItem.</summary>
    public int EquipmentItemId { get; set; }

    /// <summary>IDs of conflicting EquipmentReservation records.</summary>
    public List<int> ConflictingReservationIds { get; set; } = new();

    /// <summary>Snapshot details of conflicting reservations at alert creation time.</summary>
    public List<ReservationConflictDetail> ConflictDetails { get; set; } = new();

    /// <summary>Display name of the user alerted (operations manager / dispatcher).</summary>
    public string AlertedTo { get; set; } = string.Empty;

    /// <summary>Server-side timestamp when the alert was created.</summary>
    public DateTime AlertedAtUtc { get; set; }

    /// <summary>Whether the alert has been acknowledged by the operations manager.</summary>
    public bool IsAcknowledged { get; set; } = false;
}

/// <summary>
/// Snapshot detail for a single conflicting reservation in a SchedulingConflictAlert.
/// </summary>
public class ReservationConflictDetail
{
    public int ReservationId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public DateTime ReservationStartUtc { get; set; }
    public DateTime? ReservationEndUtc { get; set; }
}
