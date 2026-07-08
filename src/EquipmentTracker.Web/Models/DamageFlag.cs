namespace EquipmentTracker.Web.Models;

/// <summary>
/// Represents a damage / condition flag submitted for a piece of equipment.
/// Added for Issue #121 — Offline-First Mobile App for Field Workers.
///
/// OSHA compliance note (1926.95(a), 1926.20(b)(2), 1926.1412(d)(1)):
///   - DeviceTimestamp = when the worker flagged the damage (may be offline).
///   - ServerReceivedAt = when the flag arrived at the server (official OSHA record timestamp).
///   Both must be stored. If the device is permanently lost without reconnecting, the
///   ServerReceivedAt will remain null — this edge case should be addressed via a device
///   management policy (see design decision on issue #121).
/// </summary>
public class DamageFlag
{
    public int Id { get; set; }
    public int EquipmentItemId { get; set; }

    /// <summary>Text description of the damage. Photos are Phase 2 (out of MVP scope).</summary>
    public string Description { get; set; } = string.Empty;

    public int? ReportedByUserId { get; set; }

    /// <summary>Client-generated UUID — matches the DeviceTransactionId in OfflineSyncTransaction.</summary>
    public string DeviceTransactionId { get; set; } = string.Empty;

    /// <summary>UTC timestamp recorded by the device when the flag was submitted offline.</summary>
    public DateTime DeviceTimestamp { get; set; }

    /// <summary>
    /// UTC timestamp when the server processed this flag during sync.
    /// This is the official OSHA record timestamp.
    /// Null until the flag is synced.
    /// </summary>
    public DateTime? ServerReceivedAt { get; set; }
}
