namespace EquipmentTracker.Web.Models;

/// <summary>
/// A suggested alternative when a conflict is detected during reservation creation.
/// Added for Issue #123 - Project-Based Equipment Reservation & Scheduling Calendar.
/// </summary>
public class ReservationAlternative
{
    public AlternativeType Type { get; set; }

    /// <summary>Human-readable description of the suggestion.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Substitute equipment asset (Type = SubstituteAsset or DifferentSite).</summary>
    public int? SuggestedEquipmentId { get; set; }

    /// <summary>Name of the suggested equipment.</summary>
    public string? SuggestedEquipmentName { get; set; }

    /// <summary>Suggested alternative start date (Type = AdjustedDateRange).</summary>
    public DateOnly? SuggestedStartDate { get; set; }

    /// <summary>Suggested alternative end date (Type = AdjustedDateRange).</summary>
    public DateOnly? SuggestedEndDate { get; set; }

    /// <summary>Site name for DifferentSite suggestions.</summary>
    public string? SuggestedSiteName { get; set; }
}

public enum AlternativeType
{
    /// <summary>A same-category substitute asset is available for the requested dates.</summary>
    SubstituteAsset,

    /// <summary>The requested asset is available for a different (adjusted) date range.</summary>
    AdjustedDateRange,

    /// <summary>The same asset type is available at a different site.</summary>
    DifferentSite
}
