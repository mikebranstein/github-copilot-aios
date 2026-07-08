namespace EquipmentTracker.Web.Models;

/// <summary>
/// Result for a single offline transaction after server-side sync processing.
/// </summary>
public class SyncResult
{
    public string DeviceTransactionId { get; set; } = string.Empty;

    /// <summary>"success" | "conflict" | "error"</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Present only when Status == "conflict" or "error".</summary>
    public string? ConflictDetails { get; set; }

    // Added for Issue #121 — Offline-First Mobile App for Field Workers
    /// <summary>
    /// UTC timestamp when the server received and processed this transaction.
    /// For OSHA dual-timestamp compliance: this is the official server-side record timestamp.
    /// The device-side timestamp (OfflineTimestamp / DeviceTimestamp) is preserved in the
    /// corresponding CheckoutRecord or DamageFlag entity.
    /// Present on successful sync; null on conflict or error.
    /// </summary>
    public DateTime? ServerReceivedAt { get; set; }

    /// <summary>
    /// Plain-language outcome message for conflict notifications.
    /// Never a technical dialog prompt — field workers receive human-readable text only.
    /// E.g. "This item was checked out by someone else — your action was not applied."
    /// Present when Status == "conflict".
    /// </summary>
    public string? PlainLanguageMessage { get; set; }
}
