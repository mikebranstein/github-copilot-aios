namespace EquipmentTracker.Web.Models;

/// <summary>
/// Outcome of the buy vs. rent recommendation engine for an asset category.
/// Added for Issue #122.
/// </summary>
public enum RecommendationOutcome
{
    /// <summary>Not enough data yet (less than 90 days of usage/rental data).</summary>
    InsufficientData,

    /// <summary>Recommend purchasing the asset.</summary>
    Buy,

    /// <summary>Recommend continuing to rent the asset.</summary>
    ContinueRenting
}

/// <summary>
/// Generated recommendation for an asset category.
/// </summary>
public class BuyRentRecommendation
{
    public int Id { get; set; }

    /// <summary>Asset category the recommendation applies to.</summary>
    public string AssetCategory { get; set; } = string.Empty;

    public RecommendationOutcome Outcome { get; set; }

    /// <summary>Total rental cost year-to-date for this asset category.</summary>
    public decimal RentalCostYtd { get; set; }

    /// <summary>Calculated utilization rate (0–1) used for the recommendation.</summary>
    public double UtilizationRate { get; set; }

    /// <summary>
    /// The break-even ownership threshold (0–1) used for this recommendation.
    /// Default 0.70 (70 %) unless overridden per account.
    /// </summary>
    public double ThresholdUsed { get; set; }

    /// <summary>Estimated months to break even on ownership (null when InsufficientData).</summary>
    public double? BreakEvenMonths { get; set; }

    /// <summary>
    /// Number of days of data that was available when the recommendation was generated.
    /// Must be ≥ 90 for Buy or ContinueRenting outcomes.
    /// </summary>
    public int DataDaysAvailable { get; set; }

    /// <summary>MANUAL for MVP; AUTO once invoice ingestion is added.</summary>
    public string DataSource { get; set; } = "MANUAL";

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
