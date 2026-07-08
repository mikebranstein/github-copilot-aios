namespace EquipmentTracker.Web.Models;

/// <summary>
/// Configures the service interval for an equipment category (e.g., "Crane" every 250 hours).
/// Supports both hours-based and time-based (calendar) triggers per the dual-interval requirement.
/// </summary>
public class ServiceInterval
{
    public int Id { get; set; }

    /// <summary>Equipment category name this interval applies to (maps to EquipmentItem.Category).</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Whether this is an hours-based or time-based interval.</summary>
    public IntervalType IntervalType { get; set; } = IntervalType.Hours;

    /// <summary>
    /// For Hours intervals: operating hours between services (e.g., 250).
    /// For TimeBased intervals: number of days between services (e.g., 365 for annual).
    /// </summary>
    public double IntervalValue { get; set; }

    /// <summary>
    /// Number of days (time-based) or percentage-of-interval (hours-based) before due date
    /// at which the asset enters Caution status.
    /// For hours-based: Caution begins when remaining hours &lt;= 10% of interval.
    /// For time-based: Caution begins when days remaining &lt;= LeadTimeDays (default: 14).
    /// </summary>
    public int LeadTimeDays { get; set; } = 14;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
