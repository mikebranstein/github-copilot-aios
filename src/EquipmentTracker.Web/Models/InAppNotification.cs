namespace EquipmentTracker.Web.Models;

/// <summary>
/// In-app notification for reservation events (displacement, confirmation).
/// Added for Issue #123 - Project-Based Equipment Reservation & Scheduling Calendar.
/// </summary>
public class InAppNotification
{
    public int Id { get; set; }

    /// <summary>Target user (recipient).</summary>
    public int UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    /// <summary>Deep-link to the relevant reservation or planning section.</summary>
    public string? ActionUrl { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public NotificationEventType EventType { get; set; }
}

public enum NotificationEventType
{
    ReservationCreated,
    ReservationDisplaced,
    ReservationCancelled
}
