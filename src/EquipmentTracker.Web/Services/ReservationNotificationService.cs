using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-memory notification service for reservation events.
/// Simulates email delivery (logged) and persists in-app notifications.
/// Added for Issue #123 - Project-Based Equipment Reservation & Scheduling Calendar.
/// </summary>
public class ReservationNotificationService : IReservationNotificationService
{
    private readonly List<InAppNotification> _notifications = new();
    private readonly ILogger<ReservationNotificationService> _logger;
    private readonly object _lock = new();
    private int _nextId = 1;

    public ReservationNotificationService(ILogger<ReservationNotificationService> logger)
    {
        _logger = logger;
    }

    public void NotifyCreated(Reservation reservation, int recipientUserId)
    {
        var notification = new InAppNotification
        {
            Id = NextId(),
            UserId = recipientUserId,
            Title = "Reservation Confirmed",
            Message = $"Your reservation for {reservation.EquipmentName} from {reservation.StartDate:MMM d} to {reservation.EndDate:MMM d} under project \"{reservation.ProjectName}\" has been confirmed.",
            ActionUrl = $"/Reservation/Details/{reservation.Id}",
            EventType = NotificationEventType.ReservationCreated,
            CreatedAt = DateTime.UtcNow
        };

        lock (_lock) { _notifications.Add(notification); }

        // Simulate email delivery
        _logger.LogInformation(
            "[EMAIL] To: user#{UserId} | Subject: Reservation Confirmed — {EquipmentName} {StartDate}–{EndDate} | Reservation #{ReservationId}",
            recipientUserId, reservation.EquipmentName, reservation.StartDate, reservation.EndDate, reservation.Id);
    }

    public void NotifyDisplaced(Reservation displacedReservation, string conflictingProjectName, string actionUrl)
    {
        var notification = new InAppNotification
        {
            Id = NextId(),
            UserId = displacedReservation.CreatedByUserId,
            Title = "Reservation Displaced — Action Required",
            Message = $"Your reservation for {displacedReservation.EquipmentName} ({displacedReservation.StartDate:MMM d}–{displacedReservation.EndDate:MMM d}) under \"{displacedReservation.ProjectName}\" was overridden by project \"{conflictingProjectName}\". Please re-plan.",
            ActionUrl = actionUrl,
            EventType = NotificationEventType.ReservationDisplaced,
            CreatedAt = DateTime.UtcNow
        };

        lock (_lock) { _notifications.Add(notification); }

        // Simulate email delivery (SLA: within 5 minutes)
        _logger.LogWarning(
            "[EMAIL] To: user#{UserId} | Subject: URGENT — Reservation Displaced by {ConflictingProject} | {EquipmentName} {StartDate}–{EndDate} | Re-plan at {ActionUrl}",
            displacedReservation.CreatedByUserId, conflictingProjectName,
            displacedReservation.EquipmentName, displacedReservation.StartDate, displacedReservation.EndDate, actionUrl);
    }

    public void NotifyCancelled(Reservation reservation, int recipientUserId)
    {
        var notification = new InAppNotification
        {
            Id = NextId(),
            UserId = recipientUserId,
            Title = "Reservation Cancelled",
            Message = $"Your reservation for {reservation.EquipmentName} ({reservation.StartDate:MMM d}–{reservation.EndDate:MMM d}) under \"{reservation.ProjectName}\" has been cancelled.",
            ActionUrl = "/Reservation",
            EventType = NotificationEventType.ReservationCancelled,
            CreatedAt = DateTime.UtcNow
        };

        lock (_lock) { _notifications.Add(notification); }

        _logger.LogInformation(
            "[EMAIL] To: user#{UserId} | Subject: Reservation Cancelled — {EquipmentName} {StartDate}–{EndDate}",
            recipientUserId, reservation.EquipmentName, reservation.StartDate, reservation.EndDate);
    }

    public IReadOnlyList<InAppNotification> GetNotificationsForUser(int userId)
    {
        lock (_lock)
        {
            return _notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToList();
        }
    }

    public void MarkRead(int notificationId, int userId)
    {
        lock (_lock)
        {
            var n = _notifications.FirstOrDefault(n => n.Id == notificationId && n.UserId == userId);
            if (n != null) n.IsRead = true;
        }
    }

    private int NextId() { lock (_lock) { return _nextId++; } }
}
