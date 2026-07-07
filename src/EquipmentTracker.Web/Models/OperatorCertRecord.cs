namespace EquipmentTracker.Web.Models;

/// <summary>
/// An operator's individual certification record (one record per operator per cert type).
/// </summary>
public class OperatorCertRecord
{
    public int Id { get; set; }

    /// <summary>Borrower user ID if the operator has an account; null for name-only lookup.</summary>
    public int? OperatorUserId { get; set; }

    /// <summary>Full name of the operator (used as primary lookup key when UserId is absent).</summary>
    public string OperatorName { get; set; } = string.Empty;

    public int CertTypeId { get; set; }

    public DateTime IssuedDate { get; set; }
    public DateTime ExpiryDate { get; set; }

    /// <summary>Computed status refreshed by <see cref="Services.CertificationService.RefreshCertStatuses"/>.</summary>
    public CertificationStatus Status { get; set; } = CertificationStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Optional notes (e.g., issuing body, course ID).</summary>
    public string? Notes { get; set; }
}
