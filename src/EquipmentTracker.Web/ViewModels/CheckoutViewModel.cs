using System.ComponentModel.DataAnnotations;
using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

public class CheckoutViewModel
{
    public int EquipmentItemId { get; set; }
    public string EquipmentItemName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Borrower name is required.")]
    [StringLength(100)]
    public string BorrowerName { get; set; } = string.Empty;

    // ── Cert enforcement fields (Issue #120) ──────────────────────────────────

    /// <summary>Set when checkout is blocked by cert validation. Shown in the view.</summary>
    public string? CertBlockMessage { get; set; }

    /// <summary>True when the user is re-submitting with a supervisor override.</summary>
    public bool IsOverrideAttempt { get; set; } = false;

    [StringLength(100)]
    public string? OverrideSupervisorName { get; set; }

    public OverrideReasonCode OverrideReasonCode { get; set; } = OverrideReasonCode.EmergencyRenewalInProgress;

    [StringLength(500)]
    public string? OverrideReasonText { get; set; }
}
