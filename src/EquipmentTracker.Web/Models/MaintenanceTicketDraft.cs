namespace EquipmentTracker.Web.Models;

/// <summary>
/// State of a maintenance ticket draft.
/// Added for Issue #115 — Equipment Condition Assessment &amp; Damage Tracking at Return.
/// </summary>
public enum MaintenanceTicketState
{
    /// <summary>Auto-created from a Damaged return; awaiting coordinator review.</summary>
    Draft,

    /// <summary>Coordinator has acknowledged the ticket and is pending scheduling.</summary>
    PendingReview,

    /// <summary>Work order has been scheduled.</summary>
    Scheduled,

    /// <summary>Used when maintenance ticket module is unavailable; ticket is held for manual assignment.</summary>
    HoldingQueue
}

/// <summary>
/// Maintenance ticket draft auto-created when a field operator returns equipment in Damaged condition.
/// Pre-filled from the ConditionRecord; placed in Draft state for coordinator review.
/// Added for Issue #115 — Equipment Condition Assessment &amp; Damage Tracking at Return.
/// </summary>
public class MaintenanceTicketDraft
{
    public int Id { get; set; }

    /// <summary>FK to the ConditionRecord that triggered this draft.</summary>
    public int ConditionRecordId { get; set; }

    /// <summary>FK to EquipmentItem.</summary>
    public int EquipmentItemId { get; set; }

    /// <summary>Equipment name at creation time (denormalised).</summary>
    public string EquipmentName { get; set; } = string.Empty;

    /// <summary>Always Damaged for auto-created drafts.</summary>
    public ConditionGrade ConditionGrade { get; set; } = ConditionGrade.Damaged;

    /// <summary>FK to the ApplicationUser who returned the equipment.</summary>
    public int? ReportedByUserId { get; set; }

    /// <summary>Display name of the returning operator (denormalised).</summary>
    public string ReportedByName { get; set; } = string.Empty;

    /// <summary>Server-side timestamp from the ConditionRecord.</summary>
    public DateTime ReportedAtUtc { get; set; }

    /// <summary>IDs of ConditionPhoto records attached to this draft.</summary>
    public List<int> PhotoIds { get; set; } = new();

    /// <summary>Current workflow state.</summary>
    public MaintenanceTicketState State { get; set; } = MaintenanceTicketState.Draft;

    /// <summary>Whether the maintenance coordinator email notification has been sent.</summary>
    public bool NotificationSent { get; set; } = false;

    /// <summary>When the coordinator notification email was sent (UTC).</summary>
    public DateTime? NotificationSentAtUtc { get; set; }

    /// <summary>Number of notification send attempts (for retry logic).</summary>
    public int NotificationRetryCount { get; set; } = 0;
}
