namespace EquipmentTracker.Web.Models;

public class EquipmentItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;

    // Added for Issue #117 - Approval Workflow for Restricted Equipment Checkout
    /// <summary>
    /// When true, checkout requires supervisor approval before the item is released.
    /// Implements OSHA-compliant restricted equipment access control.
    /// </summary>
    public bool IsRestricted { get; set; } = false;

    /// <summary>
    /// Describes the type of approval required (e.g., "Supervisor Sign-Off", "Certification Verification").
    /// MVP supports supervisor sign-off only; cert verification is deferred.
    /// </summary>
    public string? RequiredApprovalType { get; set; }
}
