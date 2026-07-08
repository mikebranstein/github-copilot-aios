namespace EquipmentTracker.Web.Models;

/// <summary>
/// Represents a single checkout or return transaction that was performed while offline.
/// Generated on the client device and submitted during sync.
/// </summary>
public class OfflineSyncTransaction
{
    /// <summary>Client-generated UUID to identify this transaction uniquely across devices.</summary>
    public string DeviceTransactionId { get; set; } = string.Empty;

    /// <summary>"checkout", "return", or "damage_flag"</summary>
    public string Type { get; set; } = string.Empty;

    public int ItemId { get; set; }

    public int BorrowerUserId { get; set; }

    /// <summary>UTC timestamp recorded by the device when the transaction occurred offline.</summary>
    public DateTime OfflineTimestamp { get; set; }

    /// <summary>Opaque device identifier (e.g. browser fingerprint or user-agent hash).</summary>
    public string DeviceId { get; set; } = string.Empty;

    // Added for Issue #58
    public string? PhotoLocalPath { get; set; }
    public string? PhotoUploadKey { get; set; }

    // Added for Issue #114 — Bulk Checkout and Return Operations for Field Teams
    /// <summary>
    /// Groups all items in the same offline bulk operation.
    /// Null for single-item transactions. Preserved on sync as the BatchTransactionId on each CheckoutRecord.
    /// </summary>
    public string? BatchTransactionId { get; set; }

    // Added for Issue #121 — Offline-First Mobile App for Field Workers
    /// <summary>
    /// Text description of damage. Used when Type == "damage_flag".
    /// Photos are out of scope for offline MVP (Phase 2 only).
    /// </summary>
    public string? Description { get; set; }

    // Added for Issue #139 — PWA Offline Hardening (Phase 1)
    /// <summary>
    /// Free-text condition notes entered by the field worker during an offline return.
    /// Preserved through the sync queue and written to the CheckoutRecord on server-side sync.
    /// Null for checkout and damage-flag transactions.
    /// </summary>
    public string? ConditionNotes { get; set; }
}
