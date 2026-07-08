using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>
/// View model for the equipment condition history page (AC9).
/// Shows all condition records for an equipment item in reverse chronological order.
/// Added for Issue #115 — Equipment Condition Assessment &amp; Damage Tracking at Return.
/// </summary>
public class ConditionHistoryViewModel
{
    public int EquipmentItemId { get; set; }
    public string EquipmentItemName { get; set; } = string.Empty;
    public string EquipmentCategory { get; set; } = string.Empty;
    public EquipmentLifecycleStatus EquipmentStatus { get; set; }

    /// <summary>All condition records in reverse chronological order (newest first).</summary>
    public IReadOnlyList<ConditionHistoryEntry> History { get; set; } = Array.Empty<ConditionHistoryEntry>();
}

/// <summary>
/// A single entry in the condition history for an equipment item.
/// </summary>
public class ConditionHistoryEntry
{
    public int ConditionRecordId { get; set; }
    public int CheckoutRecordId { get; set; }
    public ConditionGrade Grade { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public DateTime CheckedOutAtUtc { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }
    public DateTime ConditionServerTimestampUtc { get; set; }
    public ReturnState SyncStatus { get; set; }

    /// <summary>Photos attached to this condition record.</summary>
    public IReadOnlyList<ConditionPhoto> Photos { get; set; } = Array.Empty<ConditionPhoto>();

    /// <summary>Whether a maintenance ticket was auto-created for this record.</summary>
    public bool HasMaintenanceTicket { get; set; }

    /// <summary>Whether a lost equipment flag is associated with this record.</summary>
    public bool HasLostFlag { get; set; }

    /// <summary>Whether a scheduling conflict alert was raised for this record.</summary>
    public bool HasConflictAlert { get; set; }

    /// <summary>CSS badge class for the grade, used in the view.</summary>
    public string GradeBadgeClass => Grade switch
    {
        ConditionGrade.Good => "bg-success",
        ConditionGrade.Worn => "bg-warning text-dark",
        ConditionGrade.Damaged => "bg-danger",
        ConditionGrade.Lost => "bg-dark",
        _ => "bg-secondary"
    };
}
