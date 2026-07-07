using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace EquipmentTracker.Web.ViewModels;

// ── Create / Edit ────────────────────────────────────────────────────────────

/// <summary>View model for creating a new project-linked reservation.</summary>
public class CreateReservationViewModel
{
    [Required(ErrorMessage = "Please select or create a project.")]
    public int? ProjectId { get; set; }

    // Allow inline project creation
    public string? NewProjectName { get; set; }
    public DateOnly? NewProjectStartDate { get; set; }
    public DateOnly? NewProjectEndDate { get; set; }
    public int? NewProjectSiteId { get; set; }

    [Required(ErrorMessage = "Please select at least one equipment asset.")]
    public int EquipmentId { get; set; }

    [Required(ErrorMessage = "Start date is required.")]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required(ErrorMessage = "End date is required.")]
    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(7));

    // Populated for the form
    public IReadOnlyList<Project> AvailableProjects { get; set; } = Array.Empty<Project>();
    public IReadOnlyList<EquipmentItem> AvailableEquipment { get; set; } = Array.Empty<EquipmentItem>();
    public IReadOnlyList<SiteOption> AvailableSites { get; set; } = Array.Empty<SiteOption>();
}

/// <summary>View model for editing an existing reservation.</summary>
public class EditReservationViewModel
{
    public int ReservationId { get; set; }

    [Required]
    public int EquipmentId { get; set; }

    [Required(ErrorMessage = "Start date is required.")]
    public DateOnly StartDate { get; set; }

    [Required(ErrorMessage = "End date is required.")]
    public DateOnly EndDate { get; set; }

    public IReadOnlyList<EquipmentItem> AvailableEquipment { get; set; } = Array.Empty<EquipmentItem>();
}

public class SiteOption
{
    public int SiteId { get; set; }
    public string SiteName { get; set; } = string.Empty;
}

// ── Conflict / Suggestions ───────────────────────────────────────────────────

/// <summary>Shown when one or more conflicts are detected at reservation time.</summary>
public class ReservationConflictViewModel
{
    public CreateReservationViewModel OriginalRequest { get; set; } = new();
    public IReadOnlyList<ReservationConflict> Conflicts { get; set; } = Array.Empty<ReservationConflict>();
    public bool CanOverride { get; set; }
    public string RequestedEquipmentName { get; set; } = string.Empty;
    public string RequestedProjectName { get; set; } = string.Empty;
}

// ── Calendar view ────────────────────────────────────────────────────────────

/// <summary>Data for the visual equipment reservation calendar.</summary>
public class CalendarViewModel
{
    /// <summary>Start of the displayed window (default: today − 14 days).</summary>
    public DateOnly From { get; set; }

    /// <summary>End of the displayed window (default: today + 14 days).</summary>
    public DateOnly To { get; set; }

    public int? FilterSiteId { get; set; }
    public int? FilterProjectId { get; set; }

    public IReadOnlyList<Reservation> Reservations { get; set; } = Array.Empty<Reservation>();
    public IReadOnlyList<Project> AvailableProjects { get; set; } = Array.Empty<Project>();
    public IReadOnlyList<SiteOption> AvailableSites { get; set; } = Array.Empty<SiteOption>();

    /// <summary>Distinct equipment items that appear on this calendar view.</summary>
    public IReadOnlyList<CalendarEquipmentRow> EquipmentRows { get; set; } = Array.Empty<CalendarEquipmentRow>();
}

public class CalendarEquipmentRow
{
    public int EquipmentId { get; set; }
    public string EquipmentName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public IReadOnlyList<Reservation> Reservations { get; set; } = Array.Empty<Reservation>();
}

// ── Cross-site availability ──────────────────────────────────────────────────

/// <summary>Data for the cross-site availability dashboard.</summary>
public class CrossSiteViewModel
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int WindowDays { get; set; } = 14;
    public int? FilterSiteId { get; set; }
    public IReadOnlyList<EquipmentAvailabilitySummary> Summaries { get; set; } = Array.Empty<EquipmentAvailabilitySummary>();
    public IReadOnlyList<SiteOption> AvailableSites { get; set; } = Array.Empty<SiteOption>();
}

// ── Reservation list / details ───────────────────────────────────────────────

public class ReservationListViewModel
{
    public IReadOnlyList<Reservation> MyReservations { get; set; } = Array.Empty<Reservation>();
    public IReadOnlyList<InAppNotification> Notifications { get; set; } = Array.Empty<InAppNotification>();
}

public class ReservationDetailsViewModel
{
    public Reservation Reservation { get; set; } = null!;
    public Project? Project { get; set; }
    public bool CanEdit { get; set; }
    public bool CanCancel { get; set; }
    public bool CanOverride { get; set; }
}
