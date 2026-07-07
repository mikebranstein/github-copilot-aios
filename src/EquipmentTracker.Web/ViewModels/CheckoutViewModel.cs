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

    public int? ConfirmedSiteId { get; set; }
    public string? CurrentSiteName { get; set; }
    public IReadOnlyList<SelectListItem> SiteOptions { get; set; } = [];
}
