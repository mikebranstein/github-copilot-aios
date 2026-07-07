namespace EquipmentTracker.Web.Models;

public class ApplicationUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsCoordinator { get; set; }
    public string? PushEndpoint { get; set; }
    public string? PushP256dh { get; set; }
    public string? PushAuth { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
}
