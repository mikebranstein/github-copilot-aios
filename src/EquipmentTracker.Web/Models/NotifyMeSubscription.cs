namespace EquipmentTracker.Web.Models;

/// <summary>
/// A subscription that notifies a user when a specific item or any item in a category
/// transitions to compound-Available status.
/// A subscription covers either a specific item (EquipmentItemId set) or a category (Category set).
/// Added for Issue #118 — Real-Time Equipment Availability Dashboard.
/// </summary>
public class NotifyMeSubscription
{
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>ID of the specific equipment item. Null when subscribing to a category.</summary>
    public int? EquipmentItemId { get; set; }

    /// <summary>Category name when subscribing to an entire category. Null when subscribing to a specific item.</summary>
    public string? Category { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>UTC timestamp when the user cancelled this subscription. Null = still active.</summary>
    public DateTime? CancelledAtUtc { get; set; }

    /// <summary>UTC timestamp when the notification was last fired for this subscription.</summary>
    public DateTime? LastFiredAtUtc { get; set; }

    public bool IsActive => CancelledAtUtc is null;
}
