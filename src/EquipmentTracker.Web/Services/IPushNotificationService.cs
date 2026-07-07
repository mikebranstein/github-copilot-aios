using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public interface IPushNotificationService
{
    Task SendAsync(ApplicationUser user, string title, string body);
}
