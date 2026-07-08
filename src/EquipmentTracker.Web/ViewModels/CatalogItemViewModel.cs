namespace EquipmentTracker.Web.ViewModels;

public enum EquipmentStatus
{
    Available,
    CheckedOut,
    Overdue,
    // Added for Issue #121 — Offline-First Mobile App for Field Workers
    /// <summary>
    /// Equipment has an active damage flag. Subsequent offline checkouts should reflect
    /// this status in the local cache (AC2).
    /// </summary>
    Flagged
}

public class CatalogItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public EquipmentStatus Status { get; set; }

    // Added for Issue #121 — Offline-First Mobile App for Field Workers
    /// <summary>
    /// Present when Status == Flagged. Helps the mobile app display the damage description
    /// in the local cache before sync.
    /// </summary>
    public string? FlagDescription { get; set; }
}
