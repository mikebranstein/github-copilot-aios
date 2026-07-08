namespace EquipmentTracker.Web.Models;

/// <summary>
/// Persists per-user UI preferences for the availability dashboard across sessions.
/// Added for Issue #118 — Real-Time Equipment Availability Dashboard.
/// </summary>
public class UserPreferences
{
    public int UserId { get; set; }

    /// <summary>Last selected site filter. Null = fall back to the user's AssignedSite.</summary>
    public string? PreferredSiteFilter { get; set; }

    /// <summary>Last selected category filter. Null or "All" = no category filter.</summary>
    public string? PreferredCategoryFilter { get; set; }
}
