using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-memory singleton store for coordinator notifications.
/// Persists for the lifetime of the application process only.
/// </summary>
public class CoordinatorNotificationService : ICoordinatorNotificationService
{
    private readonly List<CoordinatorNotification> _notifications = new();
    private int _nextId = 1;

    public CoordinatorNotification CreateNotification(int coordinatorUserId, int checkoutRecordId, string message)
    {
        var notification = new CoordinatorNotification
        {
            Id = _nextId++,
            CoordinatorUserId = coordinatorUserId,
            CheckoutRecordId = checkoutRecordId,
            CreatedAtUtc = DateTime.UtcNow,
            IsRead = false,
            Message = message
        };
        _notifications.Add(notification);
        return notification;
    }

    public IReadOnlyList<CoordinatorNotification> GetPendingForCoordinator(int coordinatorUserId)
    {
        return _notifications
            .Where(n => n.CoordinatorUserId == coordinatorUserId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToList()
            .AsReadOnly();
    }

    public bool MarkRead(int notificationId)
    {
        var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
        if (notification is null) return false;
        notification.IsRead = true;
        return true;
    }
}
