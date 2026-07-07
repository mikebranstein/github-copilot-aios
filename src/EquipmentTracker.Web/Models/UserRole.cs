namespace EquipmentTracker.Web.Models;

/// <summary>
/// Role-based access level for a user.
/// Added for Issue #122 — Equipment Utilization Analytics &amp; Buy vs. Rent Optimization Dashboard
/// </summary>
public enum UserRole
{
    /// <summary>Standard / read-only borrower role.</summary>
    Standard = 0,

    /// <summary>Fleet Manager: can view utilization dashboard but cannot export CFO report.</summary>
    FleetManager = 1,

    /// <summary>Operations Director: can view utilization dashboard and per-asset details.</summary>
    OperationsDirector = 2,

    /// <summary>CFO / Finance role: can view dashboard and export CFO Executive Report.</summary>
    CFO = 3,

    /// <summary>Admin: full access including CFO report export.</summary>
    Admin = 4
}
