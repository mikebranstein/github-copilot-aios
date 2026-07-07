using EquipmentTracker.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>ViewModel for the certification library management page (AC3, AC4).</summary>
public class CertLibraryViewModel
{
    public IReadOnlyList<CertificationType> CertTypes { get; set; } = [];
    public IReadOnlyList<EquipmentCategoryCertRequirement> Requirements { get; set; } = [];
    public IReadOnlyList<CertificationType> AllCertTypesForDropdown { get; set; } = [];

    /// <summary>New custom cert type form.</summary>
    public AddCertTypeForm NewCertType { get; set; } = new();

    /// <summary>New requirement assignment form.</summary>
    public AssignRequirementForm NewRequirement { get; set; } = new();

    public List<SelectListItem> CertTypeSelectList { get; set; } = [];
}

public class AddCertTypeForm
{
    [Required(ErrorMessage = "Certification name is required.")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Range(0, 36500, ErrorMessage = "Renewal period must be between 0 and 36500 days.")]
    public int RenewalPeriodDays { get; set; } = 365;
}

public class AssignRequirementForm
{
    [Required(ErrorMessage = "Equipment category is required.")]
    [StringLength(100)]
    public string EquipmentCategory { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Please select a certification type.")]
    public int CertTypeId { get; set; }
}
