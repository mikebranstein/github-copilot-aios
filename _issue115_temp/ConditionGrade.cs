namespace EquipmentTracker.Web.Models;

/// <summary>
/// Condition grade selected by the field operator during equipment return.
/// Added for Issue #115 — Equipment Condition Assessment &amp; Damage Tracking at Return.
/// </summary>
public enum ConditionGrade
{
    Good,
    Worn,
    Damaged,
    Lost
}
