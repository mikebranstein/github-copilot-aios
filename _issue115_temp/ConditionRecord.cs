namespace EquipmentTracker.Web.Models;

/// <summary>
/// Immutable condition assessment record created at equipment return.
/// Once written, rows MUST NOT be updated or deleted (DB-level / application-level immutability).
/// Added for Issue #115 — Equipment Condition Assessment &amp; Damage Tracking at Return.
/// </summary>
public class ConditionRecord
{
    public int Id { get; set; }

    /// <summary>FK to CheckoutRecord (the specific checkout/return cycle).</summary>
    public int CheckoutRecordId { get; set; }

    /// <summary>FK to EquipmentItem.</summary>
    public int EquipmentItemId { get; set; }

    /// <summary>Display name of the equipment at the time of return (denormalised for audit trail stability).</summary>
    public string EquipmentName { get; set; } = string.Empty;

    /// <summary>FK to ApplicationUser (the operator performing the return).</summary>
    public int? OperatorUserId { get; set; }

    /// <summary>Display name of the returning operator (denormalised for audit trail stability).</summary>
    public string OperatorName { get; set; } = string.Empty;

    /// <summary>Condition grade selected by the operator. Required — cannot be null.</summary>
    public ConditionGrade Grade { get; set; }

    /// <summary>
    /// Server-side timestamp applied at submission time (NOT device time).
    /// Prevents backdating. Immutable after creation.
    /// </summary>
    public DateTime ServerTimestampUtc { get; set; }

    /// <summary>Overall sync state for this return cycle.</summary>
    public ReturnState SyncStatus { get; set; } = ReturnState.Complete;

    /// <summary>Navigation: photos attached to this condition record.</summary>
    public List<ConditionPhoto> Photos { get; set; } = new();

    /// <summary>Navigation: maintenance ticket draft created for Damaged returns.</summary>
    public MaintenanceTicketDraft? MaintenanceTicketDraft { get; set; }

    /// <summary>Navigation: lost-equipment flag created for Lost returns.</summary>
    public LostEquipmentFlag? LostEquipmentFlag { get; set; }

    /// <summary>Navigation: scheduling conflict alert created for Damaged/Lost returns.</summary>
    public SchedulingConflictAlert? SchedulingConflictAlert { get; set; }
}
