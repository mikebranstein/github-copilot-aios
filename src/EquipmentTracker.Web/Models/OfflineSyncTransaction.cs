namespace EquipmentTracker.Web.Models;

/// <summary>
/// Represents a single checkout or return transaction that was performed while offline.
/// Generated on the client device and submitted during sync.
/// </summary>
public class OfflineSyncTransaction
{
    /// <summary>Client-generated UUID to identify this transaction uniquely across devices.</summary>
    public string DeviceTransactionId { get; set; } = string.Empty;

    /// <summary>"checkout" or "return"</summary>
    public string Type { get; set; } = string.Empty;

    public int ItemId { get; set; }

    public int BorrowerUserId { get; set; }

    /// <summary>UTC timestamp recorded by the device when the transaction occurred offline.</summary>
    public DateTime OfflineTimestamp { get; set; }

    /// <summary>Opaque device identifier (e.g. browser fingerprint or user-agent hash).</summary>
    public string DeviceId { get; set; } = string.Empty;

    // Added for Issue #58
    public string? PhotoLocalPath { get; set; }  // local file path before sync
    public string? PhotoUploadKey { get; set; }  // SHA-256 hash for deduplication
}
