namespace EquipmentTracker.Web.Models;

/// <summary>
/// The compound availability status for an equipment item — computed from all four conditions simultaneously.
/// Added for Issue #118 — Real-Time Equipment Availability Dashboard.
/// </summary>
public enum EquipmentCompoundStatus
{
    /// <summary>All four conditions met: not checked out, serviceable, location known, no active soft hold.</summary>
    Available,

    /// <summary>Item is currently checked out.</summary>
    CheckedOut,

    /// <summary>Item condition is Under Maintenance (LifecycleStatus = Maintenance) or Lost.</summary>
    UnderMaintenance,

    /// <summary>Item has no known site/location assignment.</summary>
    LocationUnknown,

    /// <summary>An active soft hold is on the item (reserved by another user).</summary>
    SoftHeld
}
