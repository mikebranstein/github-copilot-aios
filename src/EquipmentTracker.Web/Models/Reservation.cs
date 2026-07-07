namespace EquipmentTracker.Web.Models;

/// <summary>
/// An equipment reservation linked to a project and a date range.
/// Added for Issue #123 - Project-Based Equipment Reservation & Scheduling Calendar.
/// </summary>
public class Reservation
{
    public int Id { get; set; }

    /// <summary>The project this reservation belongs to.</summary>
    public int ProjectId { get; set; }

    /// <summary>Project display name (denormalised for calendar rendering).</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>The equipment asset being reserved.</summary>
    public int EquipmentId { get; set; }

    /// <summary>Equipment display name (denormalised for calendar rendering).</summary>
    public string EquipmentName { get; set; } = string.Empty;

    /// <summary>Equipment category (denormalised for alternative-suggestion queries).</summary>
    public string EquipmentCategory { get; set; } = string.Empty;

    /// <summary>Site the equipment belongs to.</summary>
    public int SiteId { get; set; }

    /// <summary>Site name (denormalised).</summary>
    public string SiteName { get; set; } = string.Empty;

    /// <summary>Inclusive start of the reservation.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Inclusive end of the reservation.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>User who created the reservation.</summary>
    public int CreatedByUserId { get; set; }

    /// <summary>Username of creator.</summary>
    public string CreatedByUserName { get; set; } = string.Empty;

    public ReservationStatus Status { get; set; } = ReservationStatus.Active;

    public DateTime? CancelledAt { get; set; }

    /// <summary>User ID of the operations manager who overrode this reservation (if applicable).</summary>
    public int? OverriddenByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
