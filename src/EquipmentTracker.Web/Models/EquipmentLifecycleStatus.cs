namespace EquipmentTracker.Web.Models;

/// <summary>
/// Equipment lifecycle/inventory status, including damage and loss states.
/// Added for Issue #115 — Equipment Condition Assessment &amp; Damage Tracking at Return.
/// Note: named EquipmentLifecycleStatus to avoid conflict with the existing
///       EquipmentTracker.Web.ViewModels.EquipmentStatus view enum.
/// </summary>
public enum EquipmentLifecycleStatus
{
    Available,
    CheckedOut,
    Maintenance,
    Lost
}
