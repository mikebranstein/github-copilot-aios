using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public interface IUserService
{
    ApplicationUser? Register(string username, string password, bool isCoordinator = false);
    ApplicationUser? GetByUsername(string username);
    ApplicationUser? GetById(int id);
    bool ValidatePassword(ApplicationUser user, string password);
    IReadOnlyList<ApplicationUser> GetCoordinators();
    IReadOnlyList<ApplicationUser> GetBorrowers();
    void UpdatePushSubscription(int userId, string? endpoint, string? p256dh, string? auth);
    void SetNotificationsEnabled(int userId, bool enabled);

    // Added for Issue #117 - Approval Workflow for Restricted Equipment Checkout
    /// <summary>Returns all users with the Safety Admin role.</summary>
    IReadOnlyList<ApplicationUser> GetSafetyAdmins();

    /// <summary>Returns all users designated as approvers (coordinators who can approve restricted checkouts).</summary>
    IReadOnlyList<ApplicationUser> GetApprovers();

    /// <summary>Grants or revokes the Safety Admin role. Must be called by an account admin. Audited separately.</summary>
    void SetSafetyAdmin(int userId, bool isSafetyAdmin);
}
