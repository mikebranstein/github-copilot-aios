using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>
/// Represents the availability state of one equipment item on the dashboard.
/// Added for Issue #118 — Real-Time Equipment Availability Dashboard.
/// </summary>
public class EquipmentAvailabilityItem
{
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public EquipmentCompoundStatus CompoundStatus { get; set; }

    /// <summary>Human-readable reason shown for unavailable items (e.g. "Checked Out", "Under Maintenance", "Soft Hold — expires in 22 min").</summary>
    public string? BlockingReason { get; set; }

    /// <summary>ID of the active soft hold (if CompoundStatus == SoftHeld).</summary>
    public int? ActiveSoftHoldId { get; set; }

    /// <summary>Remaining minutes on the active soft hold (0 if none).</summary>
    public int SoftHoldRemainingMinutes { get; set; }

    /// <summary>User ID of the current soft hold owner (null if none).</summary>
    public int? SoftHoldOwnerUserId { get; set; }
}

/// <summary>
/// Category-level summary: how many items are available out of the total count.
/// </summary>
public class CategoryAvailabilitySummary
{
    public string Category { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    public int AvailableCount { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<EquipmentAvailabilityItem> Items { get; set; } = Array.Empty<EquipmentAvailabilityItem>();
}

/// <summary>
/// Top-level view model for the availability dashboard.
/// </summary>
public class AvailabilityDashboardViewModel
{
    public IReadOnlyList<CategoryAvailabilitySummary> Categories { get; set; } = Array.Empty<CategoryAvailabilitySummary>();

    /// <summary>UTC timestamp when the data was last computed.</summary>
    public DateTime DataFreshnessUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Minutes since last refresh. Used to drive the staleness warning at > 5 minutes.</summary>
    public double MinutesSinceRefresh => (DateTime.UtcNow - DataFreshnessUtc).TotalMinutes;

    /// <summary>True when data is more than 5 minutes stale — triggers the visible staleness warning.</summary>
    public bool IsStale => MinutesSinceRefresh > 5;

    /// <summary>The active site filter applied to this view.</summary>
    public string? SiteFilter { get; set; }

    /// <summary>The active category filter applied to this view. Null or "All" = no category filter.</summary>
    public string? CategoryFilter { get; set; }

    /// <summary>All distinct site names available for the site-filter dropdown.</summary>
    public IReadOnlyList<string> AvailableSites { get; set; } = Array.Empty<string>();

    /// <summary>All distinct category names available for the category-filter dropdown.</summary>
    public IReadOnlyList<string> AvailableCategories { get; set; } = Array.Empty<string>();

    /// <summary>Active Notify Me subscriptions for the current user.</summary>
    public IReadOnlyList<NotifyMeSubscription> MySubscriptions { get; set; } = Array.Empty<NotifyMeSubscription>();

    /// <summary>True when the user is viewing in offline mode (connectivity lost).</summary>
    public bool IsOffline { get; set; }
}
