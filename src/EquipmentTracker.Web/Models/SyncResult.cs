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

    /// <summary>
    /// UTC timestamp when the server received and processed this transaction.
    /// Present on successful sync; null on conflict or error.
    /// </summary>
    public DateTime? ServerReceivedAt { get; set; }

    /// <summary>
    /// Plain-language outcome message for field-worker conflict notifications.
    /// Never a technical prompt.
    /// </summary>
    public string? PlainLanguageMessage { get; set; }

    /// <summary>
    /// Human-readable feedback message for the PWA conflict UI.
    /// </summary>
    public string? WorkerMessage { get; set; }
}
