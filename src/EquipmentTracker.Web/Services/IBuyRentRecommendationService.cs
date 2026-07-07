using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Generates buy vs. rent recommendations for asset categories.
///
/// Rules (per approved design &amp; AC-3, AC-4):
///   - Minimum 90 days of data required; below this threshold outcome = InsufficientData.
///   - Default break-even threshold: 70 % utilization (configurable per account).
///   - When utilization ≥ threshold AND rental cost is significant: recommend Buy.
///   - Otherwise recommend ContinueRenting.
///   - Data source is always noted (MANUAL for MVP).
/// </summary>
public interface IBuyRentRecommendationService
{
    /// <summary>
    /// Evaluates a recommendation for the given asset category.
    /// </summary>
    BuyRentRecommendation Evaluate(string assetCategory, DateTime asOf, double breakEvenThreshold = 0.70);

    /// <summary>
    /// Evaluates recommendations for all unique asset categories present in the system.
    /// </summary>
    IReadOnlyList<BuyRentRecommendation> EvaluateAll(DateTime asOf, double breakEvenThreshold = 0.70);
}
