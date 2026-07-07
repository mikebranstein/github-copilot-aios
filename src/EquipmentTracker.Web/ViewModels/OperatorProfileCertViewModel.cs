using EquipmentTracker.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>ViewModel for the operator certification profile page (AC5, AC7).</summary>
public class OperatorProfileViewModel
{
    public string OperatorName { get; set; } = string.Empty;

    public IReadOnlyList<OperatorCertRecord> CertRecords { get; set; } = [];
    public IReadOnlyList<CertificationType> CertTypes { get; set; } = [];
    public Dictionary<int, CertificationType> CertTypeById { get; set; } = [];
    public Dictionary<int, IReadOnlyList<CertDocument>> DocumentsByCertRecordId { get; set; } = [];

    /// <summary>Form to add a new cert record.</summary>
    public AddCertRecordForm NewCertRecord { get; set; } = new();
    public List<SelectListItem> CertTypeSelectList { get; set; } = [];

    /// <summary>Form to upload a document for a cert record.</summary>
    public UploadDocumentForm NewDocument { get; set; } = new();
}

public class AddCertRecordForm
{
    [Required] public string OperatorName { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Please select a certification type.")]
    public int CertTypeId { get; set; }

    [Required(ErrorMessage = "Issued date is required.")]
    public DateTime IssuedDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Expiry date is required.")]
    public DateTime ExpiryDate { get; set; } = DateTime.Today.AddYears(1);

    [StringLength(500)] public string? Notes { get; set; }
}

public class UploadDocumentForm
{
    [Required] public int CertRecordId { get; set; }
    public IFormFile? File { get; set; }
    [Required] public string UploadedBy { get; set; } = string.Empty;
}
