using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-app (and simulated email) notification service for reservation events.
/// Added for Issue #123 - Project-Based Equipment Reservation & Scheduling Calendar.
/// </summary>
public interface IReservationNotificationService
{
    /// <summary>Send creation confirmation to the creating user.</summary>
    void NotifyCreated(Reservation reservation, int recipientUserId);

    /// <summary>
    /// Notify the displaced reservation owner about an override.
    /// Must be sent within 5 minutes of override confirmation (SLA from BA).
    /// </summary>
    void NotifyDisplaced(Reservation displacedReservation, string conflictingProjectName, string actionUrl);

    /// <summary>Notify the owner that their reservation was cancelled.</summary>
    void NotifyCancelled(Reservation reservation, int recipientUserId);

    IReadOnlyList<InAppNotification> GetNotificationsForUser(int userId);
    void MarkRead(int notificationId, int userId);
}
