namespace EquipmentTracker.Web.Models;

/// <summary>
/// Immutable record created when a supervisor overrides a blocked checkout.
/// Append-only — no UPDATE or DELETE is permitted post-creation.
/// Retrievable for OSHA inspection at any time.
/// </summary>
public class CheckoutOverrideRecord
{
    public int Id { get; set; }

    /// <summary>The checkout record that was blocked and then permitted via this override.</summary>
    public int CheckoutRecordId { get; set; }

    /// <summary>Full name of the authorising supervisor.</summary>
    public string SupervisorName { get; set; } = string.Empty;

    /// <summary>User ID of the supervisor, if available.</summary>
    public int? SupervisorUserId { get; set; }

    public OverrideReasonCode ReasonCode { get; set; }

    /// <summary>Free-text elaboration on the reason (optional but encouraged).</summary>
    public string ReasonText { get; set; } = string.Empty;

    /// <summary>Name of the operator whose checkout was blocked.</summary>
    public string BlockedOperatorName { get; set; } = string.Empty;

    /// <summary>Name of the certification that triggered the block.</summary>
    public string RequiredCertName { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the override — immutable after creation.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
