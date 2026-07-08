namespace EquipmentTracker.Web.Models;

/// <summary>
/// State of an equipment return with respect to offline photo sync.
/// Added for Issue #115 — Equipment Condition Assessment &amp; Damage Tracking at Return.
/// </summary>
public enum ReturnState
{
    /// <summary>Return is fully complete — all data (including photos) has been persisted.</summary>
    Complete,

    /// <summary>Return record is saved but one or more photos are still pending offline upload.</summary>
    PendingPhotoSync
}
