namespace EquipmentTracker.Web.Models;

/// <summary>Service status bands for the Smart Maintenance Scheduling dashboard.</summary>
public enum MaintenanceBand
{
    /// <summary>Asset has no checkout history or no service interval configured.</summary>
    NoData,

    /// <summary>More than 10% of interval remaining — no action required.</summary>
    InRange,

    /// <summary>Within 10% of service interval, or within lead-time days for a time-based interval.</summary>
    Caution,

    /// <summary>Interval exceeded with no logged service event since last reset.</summary>
    Overdue
}
