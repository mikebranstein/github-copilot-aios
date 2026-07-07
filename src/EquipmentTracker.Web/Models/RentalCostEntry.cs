namespace EquipmentTracker.Web.Models;

/// <summary>
/// Manually-entered rental cost for an asset category during a given period.
/// For Issue #122 — MVP rental costs are entered manually (P3 will add automated invoice ingestion).
/// </summary>
public class RentalCostEntry
{
    public int Id { get; set; }

    /// <summary>Asset category (e.g. "Excavator"). Matches EquipmentItem.Category.</summary>
    public string AssetCategory { get; set; } = string.Empty;

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    /// <summary>Total rental cost for this period in the account's currency.</summary>
    public decimal CostAmount { get; set; }

    public string Currency { get; set; } = "USD";

    /// <summary>Username of the user who entered this record.</summary>
    public string EnteredBy { get; set; } = string.Empty;

    /// <summary>Always MANUAL for MVP (automated ingestion is P3).</summary>
    public string EntrySource { get; set; } = "MANUAL";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
