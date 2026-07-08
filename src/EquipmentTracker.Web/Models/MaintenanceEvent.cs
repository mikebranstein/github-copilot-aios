namespace EquipmentTracker.Web.Models;

/// <summary>
/// A structured maintenance service event logged against an asset.
/// Records must be retained for a minimum of 7 years (OSHA requirement).
/// </summary>
public class MaintenanceEvent
{
    public int Id { get; set; }

    public int AssetId { get; set; }

    /// <summary>Type of service performed (e.g., "Oil Change", "Tire Rotation", "Annual Inspection").</summary>
    public string EventType { get; set; } = string.Empty;

    public DateTime EventDate { get; set; }

    /// <summary>Operating hours on the asset at the time of service (hours-based intervals).</summary>
    public double HoursAtService { get; set; }

    /// <summary>Optional: name of the technician who performed the service.</summary>
    public string? TechnicianName { get; set; }

    /// <summary>Optional: free-text notes about the service event.</summary>
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Retention expiry date — must not be purged before this date (7-year OSHA requirement).</summary>
    public DateTime RetentionExpiresAtUtc => CreatedAtUtc.AddYears(7);
}
