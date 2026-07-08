namespace EquipmentTracker.Web.Models;

/// <summary>
/// Per-asset alert configuration, including snooze state and notification recipients.
/// </summary>
public class AlertConfig
{
    public int Id { get; set; }

    public int AssetId { get; set; }

    /// <summary>
    /// When non-null, alerts for this asset are suppressed until this UTC timestamp.
    /// Users may snooze for 7, 14, or 30 days.
    /// </summary>
    public DateTime? SnoozedUntilUtc { get; set; }

    /// <summary>Comma-separated email addresses for alert recipients.</summary>
    public string NotificationRecipients { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Returns true if the alert is currently snoozed.</summary>
    public bool IsSnoozed => SnoozedUntilUtc.HasValue && SnoozedUntilUtc.Value > DateTime.UtcNow;
}
