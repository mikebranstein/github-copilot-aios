namespace EquipmentTracker.Web.Models;

/// <summary>
/// Represents a construction/field project that equipment can be reserved against.
/// Added for Issue #123 - Project-Based Equipment Reservation & Scheduling Calendar.
/// </summary>
public class Project
{
    public int Id { get; set; }

    /// <summary>Project display name (e.g., "Site Alpha – Phase 2").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Inclusive start date of the project.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Inclusive end date of the project.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Site/location identifier (matches EquipmentItem.SiteId for cross-site queries).</summary>
    public int SiteId { get; set; }

    /// <summary>Site display name (denormalised for convenience).</summary>
    public string SiteName { get; set; } = string.Empty;

    /// <summary>User ID of the project owner (creator).</summary>
    public int OwnerId { get; set; }

    /// <summary>Username of the project owner.</summary>
    public string OwnerName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
