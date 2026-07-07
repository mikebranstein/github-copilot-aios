namespace EquipmentTracker.Web.Models;

/// <summary>
/// Links a certification type to an equipment category as a mandatory requirement.
/// When active, any checkout attempt for the category will trigger cert validation.
/// </summary>
public class EquipmentCategoryCertRequirement
{
    public int Id { get; set; }

    /// <summary>Equipment category string (matches <see cref="EquipmentItem.Category"/>).</summary>
    public string EquipmentCategory { get; set; } = string.Empty;

    public int CertTypeId { get; set; }

    /// <summary>False means the requirement exists but is temporarily suspended.</summary>
    public bool IsActive { get; set; } = true;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
