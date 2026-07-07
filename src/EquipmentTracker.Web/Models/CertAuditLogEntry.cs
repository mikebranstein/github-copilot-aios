namespace EquipmentTracker.Web.Models;

/// <summary>
/// Append-only audit trail for all certification-related events:
/// cert status changes, document uploads, supervisor overrides, expiry transitions, and bulk imports.
/// Rows are never edited or deleted.
/// </summary>
public class CertAuditLogEntry
{
    public int Id { get; set; }

    /// <summary>Entity type: "OperatorCertRecord", "CertDocument", "CheckoutOverrideRecord", "BulkImport".</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Primary key of the affected entity.</summary>
    public int EntityId { get; set; }

    /// <summary>Name or ID of the user who triggered the event.</summary>
    public string ActorName { get; set; } = string.Empty;

    /// <summary>Event type: "CertAdded", "CertExpired", "DocumentUploaded", "OverrideRecorded", "BulkImport", "CertStatusChanged".</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>JSON or text description of state before the event (null for create events).</summary>
    public string? BeforeState { get; set; }

    /// <summary>JSON or text description of state after the event.</summary>
    public string? AfterState { get; set; }

    /// <summary>UTC timestamp — immutable after creation.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
