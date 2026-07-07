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
}
