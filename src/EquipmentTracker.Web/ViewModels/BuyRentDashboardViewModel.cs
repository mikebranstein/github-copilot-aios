using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>
/// ViewModel for the buy vs. rent recommendations page (AC-3, AC-4).
/// </summary>
public class BuyRentDashboardViewModel
{
    public IReadOnlyList<BuyRentRecommendation> Recommendations { get; set; } = [];

    public IReadOnlyList<BuyRentRecommendation> ActionableRecommendations =>
        Recommendations
            .Where(r => r.Outcome != RecommendationOutcome.InsufficientData)
            .ToList()
            .AsReadOnly();

    public IReadOnlyList<BuyRentRecommendation> InsufficientDataItems =>
        Recommendations
            .Where(r => r.Outcome == RecommendationOutcome.InsufficientData)
            .ToList()
            .AsReadOnly();

    /// <summary>Configured break-even threshold for this view (default 0.70).</summary>
    public double BreakEvenThreshold { get; set; } = 0.70;
}
