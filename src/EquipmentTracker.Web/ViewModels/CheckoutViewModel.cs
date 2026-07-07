using Microsoft.AspNetCore.Mvc.Rendering;
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

    // Added for Issue #117 - Approval Workflow for Restricted Equipment Checkout
    /// <summary>True if the item is marked restricted and requires approval.</summary>
    public bool IsRestricted { get; set; } = false;

    /// <summary>Describes the type of approval required (e.g., "Supervisor Sign-Off").</summary>
    public string? RequiredApprovalType { get; set; }

    /// <summary>Optional: requested checkout duration, included in the approver push notification.</summary>
    [StringLength(100)]
    public string? CheckoutDuration { get; set; }
}
