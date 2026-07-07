using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public interface ICoordinatorNotificationService
{
    CoordinatorNotification CreateNotification(int coordinatorUserId, int checkoutRecordId, string message);
    IReadOnlyList<CoordinatorNotification> GetPendingForCoordinator(int coordinatorUserId);
    bool MarkRead(int notificationId);
}
