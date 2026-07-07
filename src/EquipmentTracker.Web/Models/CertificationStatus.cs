namespace EquipmentTracker.Web.Models;

/// <summary>Lifecycle status of an operator certification record.</summary>
public enum CertificationStatus
{
    /// <summary>Certification is current and valid.</summary>
    Active,

    /// <summary>Certification expires within 30 days.</summary>
    ExpiringSoon,

    /// <summary>Certification has passed its expiry date.</summary>
    Expired,

    /// <summary>No certification record exists for this operator/type pair.</summary>
    Missing
}
