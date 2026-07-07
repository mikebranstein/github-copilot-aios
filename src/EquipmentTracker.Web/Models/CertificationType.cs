namespace EquipmentTracker.Web.Models;

/// <summary>
/// A certification type in the certification library (e.g., "Forklift Operator Certification").
/// Pre-seeded types may not be deleted while any operator record references them.
/// </summary>
public class CertificationType
{
    public int Id { get; set; }

    /// <summary>Display name of the certification (e.g., "OSHA 30 Construction").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Description / scope of the certification.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Default renewal period in days (e.g., 1825 = 5 years). 0 = never expires.</summary>
    public int RenewalPeriodDays { get; set; }

    /// <summary>True for the 15 pre-seeded OSHA/construction cert types.</summary>
    public bool IsPreSeeded { get; set; }

    /// <summary>False while any OperatorCertRecord references this type.</summary>
    public bool IsDeletable { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
