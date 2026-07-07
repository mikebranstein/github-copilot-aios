namespace EquipmentTracker.Web.Models;

public class ApplicationUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsCoordinator { get; set; }
    // Added for Issue #123 - Project-Based Equipment Reservation & Scheduling Calendar
    /// <summary>
    /// Operations Manager role: can override conflicting reservations and view cross-site dashboards.
    /// Must be explicitly assigned by an account admin.
    /// </summary>
    public bool IsOperationsManager { get; set; } = false;

    /// <summary>
    /// Primary site assignment for site supervisors (0 = no restriction / multi-site access).
    /// Operations Managers and Admins see all sites regardless of this value.
    /// </summary>
    public int SiteId { get; set; } = 0;

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
}
