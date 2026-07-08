namespace EquipmentTracker.Web.Models;

public class ApplicationUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsCoordinator { get; set; }

    // Added for Issue #117 - Approval Workflow for Restricted Equipment Checkout
    /// <summary>
    /// Safety Admin role: only Safety Admins can invoke emergency override.
    /// Must be explicitly assigned by an account admin — not auto-assigned.
    /// </summary>
    public bool IsSafetyAdmin { get; set; } = false;

    public string? PushEndpoint { get; set; }
    public string? PushP256dh { get; set; }
    public string? PushAuth { get; set; }
    public bool NotificationsEnabled { get; set; } = true;

    // Added for Issue #122 — Role-based access for utilization analytics
    /// <summary>
    /// Fine-grained role controlling access to analytics features.
    /// Admin and CFO roles can export the CFO Executive Report.
    /// FleetManager and OperationsDirector can view the utilization dashboard.
    /// </summary>
    public UserRole Role { get; set; } = UserRole.Standard;

    /// <summary>
    /// Returns true if this user may export the CFO Executive Report.
    /// </summary>
    public bool CanExportCfoReport => Role == UserRole.CFO || Role == UserRole.Admin;

    /// <summary>
    /// Returns true if this user may view the utilization dashboard.
    /// </summary>
    public bool CanViewUtilizationDashboard =>
        Role == UserRole.FleetManager ||
        Role == UserRole.OperationsDirector ||
        Role == UserRole.CFO ||
        Role == UserRole.Admin;

    // Added for Issue #118 — Real-Time Equipment Availability Dashboard
    /// <summary>The site/job this field manager is primarily assigned to. Used as the default site filter on the availability dashboard.</summary>
    public string? AssignedSite { get; set; }

    /// <summary>Phone number for SMS fallback notifications (Notify Me feature). E.164 format preferred.</summary>
    public string? PhoneNumber { get; set; }
}
