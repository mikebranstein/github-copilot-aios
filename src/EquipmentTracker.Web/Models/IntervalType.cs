namespace EquipmentTracker.Web.Models;

/// <summary>How a service interval is measured.</summary>
public enum IntervalType
{
    /// <summary>Interval is measured in operating hours derived from checkout/return records.</summary>
    Hours,

    /// <summary>Interval is measured in calendar days (e.g., annual inspection).</summary>
    TimeBased
}
