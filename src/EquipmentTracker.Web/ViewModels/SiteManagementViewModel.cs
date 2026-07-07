using EquipmentTracker.Web.Models;
using System.ComponentModel.DataAnnotations;

namespace EquipmentTracker.Web.ViewModels;

public class SiteManagementViewModel
{
    public IReadOnlyList<Site> Sites { get; init; } = [];
}

public class CreateSiteViewModel
{
    [Required(ErrorMessage = "Site name is required.")]
    [StringLength(100, MinimumLength = 1)]
    [Display(Name = "Site Name")]
    public string Name { get; set; } = string.Empty;
}

public class RenameSiteViewModel
{
    public int SiteId { get; set; }

    [Required(ErrorMessage = "Site name is required.")]
    [StringLength(100, MinimumLength = 1)]
    [Display(Name = "New Name")]
    public string NewName { get; set; } = string.Empty;
}
