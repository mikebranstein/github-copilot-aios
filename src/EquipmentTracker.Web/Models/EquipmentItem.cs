namespace EquipmentTracker.Web.Models;

public class EquipmentItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Available;
    public int? SiteId { get; set; }

    // Added for Issue #118 — Real-Time Equipment Availability Dashboard
    /// <summary>Site or job-site this item is currently assigned to. Null/empty = unknown location (blocks compound availability).</summary>
    public string SiteName { get; set; } = string.Empty;
    public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public EquipmentLifecycleStatus LifecycleStatus { get; set; } = EquipmentLifecycleStatus.Available;

    // Added for Issue #117 - Approval Workflow for Restricted Equipment Checkout
    /// <summary>
    /// When true, checkout requires supervisor approval before the item is released.
    /// Implements OSHA-compliant restricted equipment access control.
    /// </summary>
    public bool IsRestricted { get; set; }

    /// <summary>
    /// Describes the type of approval required (e.g., "Supervisor Sign-Off", "Certification Verification").
    /// MVP supports supervisor sign-off only; cert verification is deferred.
    /// </summary>
    public string? RequiredApprovalType { get; set; }
}
