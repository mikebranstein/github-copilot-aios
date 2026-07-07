using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

// In-memory user store. Data is lost on application restart.
public class UserService : IUserService
{
    private readonly List<ApplicationUser> _users = new();
    private int _nextId = 1;

    public ApplicationUser? Register(string username, string password, bool isCoordinator = false)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;
        if (_users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            return null;

        var user = new ApplicationUser
        {
            Id = _nextId++,
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsCoordinator = isCoordinator,
            NotificationsEnabled = true
        };
        _users.Add(user);
        return user;
    }

    public ApplicationUser? GetByUsername(string username) =>
        _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

    public ApplicationUser? GetById(int id) =>
        _users.FirstOrDefault(u => u.Id == id);

    public bool ValidatePassword(ApplicationUser user, string password) =>
        BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

    public IReadOnlyList<ApplicationUser> GetCoordinators() =>
        _users.Where(u => u.IsCoordinator).ToList().AsReadOnly();

    public void UpdatePushSubscription(int userId, string? endpoint, string? p256dh, string? auth)
    {
        var user = GetById(userId);
        if (user is null) return;
        user.PushEndpoint = endpoint;
        user.PushP256dh = p256dh;
        user.PushAuth = auth;
    }

    public void SetNotificationsEnabled(int userId, bool enabled)
    {
        var user = GetById(userId);
        if (user is null) return;
        user.NotificationsEnabled = enabled;
    }
}
