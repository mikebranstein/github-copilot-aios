using EquipmentTracker.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

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
    public bool IsOverrideAttempt { get; set; }

    [StringLength(100)]
    public string? OverrideSupervisorName { get; set; }

    public OverrideReasonCode OverrideReasonCode { get; set; } = OverrideReasonCode.EmergencyRenewalInProgress;

    [StringLength(500)]
    public string? OverrideReasonText { get; set; }
    public int? ConfirmedSiteId { get; set; }
    public string? CurrentSiteName { get; set; }
    public IReadOnlyList<SelectListItem> SiteOptions { get; set; } = [];

    public bool IsRestricted { get; set; }
    public string? RequiredApprovalType { get; set; }
    
    /// <summary>Optional: requested checkout duration, included in the approver push notification.</summary>
    [StringLength(100)]
    public string? CheckoutDuration { get; set; }
}
