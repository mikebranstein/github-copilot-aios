namespace EquipmentTracker.Web.Models;

// Added for Issue #117 - Approval Workflow for Restricted Equipment Checkout

/// <summary>
/// Immutable, append-only audit log entry for restricted equipment checkout decisions.
/// OSHA 1910.178 / 29 CFR 1926 Subpart CC compliance: no UPDATE/DELETE ever written to this table.
/// Each decision event (approve, deny, escalate, emergency_override) produces one entry.
/// </summary>
public class RestrictedAuditLogEntry
{
    public int Id { get; set; }

    /// <summary>User ID of the person who submitted the checkout request.</summary>
    public int RequestorId { get; set; }

    /// <summary>User ID of the approver/denier/override user. Null for system-generated escalation events.</summary>
    public int? ApproverId { get; set; }

    /// <summary>Equipment item ID.</summary>
    public int EquipmentId { get; set; }

    /// <summary>UTC timestamp when the checkout request was originally submitted.</summary>
    public DateTime CheckoutRequestedAt { get; set; }

    /// <summary>Server-side UTC timestamp when this audit entry was written. Immutable after creation.</summary>
    public DateTime DecisionMadeAt { get; set; }

    /// <summary>The decision recorded in this entry.</summary>
    public AuditDecision Decision { get; set; }

    /// <summary>Mandatory free-text reason if Decision == Denied (minimum 10 characters).</summary>
    public string? DenialReason { get; set; }

    /// <summary>True when a Safety Admin invoked emergency override bypass.</summary>
    public bool EmergencyOverrideFlag { get; set; } = false;

    /// <summary>Mandatory reason text when EmergencyOverrideFlag is true.</summary>
    public string? OverrideReason { get; set; }

    /// <summary>User ID of the Safety Admin who invoked override, if applicable.</summary>
    public int? OverridingUserId { get; set; }

    /// <summary>Approval request ID this entry is linked to.</summary>
    public int ApprovalRequestId { get; set; }
}

/// <summary>Immutable decision type recorded in the audit log.</summary>
public enum AuditDecision
{
    Approved,
    Denied,
    Escalated,
    EmergencyOverride
}
