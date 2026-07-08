namespace EquipmentTracker.Web.Models;

/// <summary>
/// Tracks equipment items that have been flagged as Lost by the return flow.
/// Equipment flagged Lost CANNOT be reactivated to Available without an explicit admin action.
/// Added for Issue #115 — Equipment Condition Assessment &amp; Damage Tracking at Return.
/// </summary>
public class LostEquipmentFlag
{
    public int Id { get; set; }

    /// <summary>FK to EquipmentItem.</summary>
    public int EquipmentItemId { get; set; }

    /// <summary>FK to the ConditionRecord that triggered this flag.</summary>
    public int ConditionRecordId { get; set; }

    /// <summary>FK to the ApplicationUser who reported the item Lost.</summary>
    public int? FlaggedByUserId { get; set; }

    /// <summary>Display name of the reporting operator (denormalised).</summary>
    public string FlaggedByName { get; set; } = string.Empty;

    /// <summary>Server-side timestamp when the Lost flag was set.</summary>
    public DateTime FlaggedAtUtc { get; set; }

    /// <summary>FK to the ApplicationUser who performed the admin reactivation (null if still Lost).</summary>
    public int? ReactivatedByUserId { get; set; }

    /// <summary>Timestamp of admin-initiated reactivation (null if still Lost).</summary>
    public DateTime? ReactivatedAtUtc { get; set; }

    /// <summary>Whether the ops director / finance contact has been notified.</summary>
    public bool NotificationSent { get; set; } = false;

    /// <summary>When the Lost notification was sent (UTC).</summary>
    public DateTime? NotificationSentAtUtc { get; set; }
}
