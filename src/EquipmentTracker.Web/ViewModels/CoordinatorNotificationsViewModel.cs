using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

public class CoordinatorNotificationsViewModel
{
    public IReadOnlyList<CoordinatorNotification> Notifications { get; set; } = [];
}
