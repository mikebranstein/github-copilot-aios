using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-memory implementation of IBuyRentRecommendationService.
///
/// Break-even estimation (simplified MVP):
///   If the rental YTD cost over the elapsed YTD months implies an annualised rental cost,
///   we estimate break-even ownership months = AnnualisedRentalCost / (AnnualisedRentalCost * utilizationRate).
///   In practice the formula reduces to: BreakEvenMonths ≈ 12 / utilizationRate
///   (i.e. at 100 % utilization it pays off in ~1 year; at 70 % in ~17 months).
///   This is a directional estimate suitable for the MVP recommendation card.
/// </summary>
public class BuyRentRecommendationService : IBuyRentRecommendationService
{
    private const int MinimumDataDays = 90;

    private readonly IEquipmentService _equipmentService;
    private readonly IUtilizationService _utilizationService;
    private readonly IRentalCostService _rentalCostService;

    public BuyRentRecommendationService(
        IEquipmentService equipmentService,
        IUtilizationService utilizationService,
        IRentalCostService rentalCostService)
    {
        _equipmentService = equipmentService;
        _utilizationService = utilizationService;
        _rentalCostService = rentalCostService;
    }

    public BuyRentRecommendation Evaluate(string assetCategory, DateTime asOf, double breakEvenThreshold = 0.70)
    {
        // Determine days of data available (rental + utilization)
        int rentalDays = _rentalCostService.GetDataDaysAvailable(assetCategory, asOf);
        int utilizDays = GetUtilizationDataDays(assetCategory, asOf);
        int dataDays = Math.Max(rentalDays, utilizDays);

        if (dataDays < MinimumDataDays)
        {
            return new BuyRentRecommendation
            {
                AssetCategory = assetCategory,
                Outcome = RecommendationOutcome.InsufficientData,
                RentalCostYtd = 0,
                UtilizationRate = 0,
                ThresholdUsed = breakEvenThreshold,
                BreakEvenMonths = null,
                DataDaysAvailable = dataDays,
                DataSource = "MANUAL",
                GeneratedAt = asOf
            };
        }

        // Compute average utilization across all assets in this category over trailing 90 days
        double utilizationRate = GetCategoryUtilization(assetCategory, asOf);
        decimal rentalCostYtd = _rentalCostService.GetYtdCost(assetCategory, asOf);

        // Break-even months estimate: 12 / utilization rate (capped at 120 months / 10 years)
        double? breakEvenMonths = utilizationRate > 0
            ? Math.Min(120, 12.0 / utilizationRate)
            : (double?)null;

        var outcome = utilizationRate >= breakEvenThreshold
            ? RecommendationOutcome.Buy
            : RecommendationOutcome.ContinueRenting;

        return new BuyRentRecommendation
        {
            AssetCategory = assetCategory,
            Outcome = outcome,
            RentalCostYtd = rentalCostYtd,
            UtilizationRate = utilizationRate,
            ThresholdUsed = breakEvenThreshold,
            BreakEvenMonths = breakEvenMonths,
            DataDaysAvailable = dataDays,
            DataSource = "MANUAL",
            GeneratedAt = asOf
        };
    }

    public IReadOnlyList<BuyRentRecommendation> EvaluateAll(DateTime asOf, double breakEvenThreshold = 0.70)
    {
        var categories = _equipmentService.GetAllItems()
            .Select(i => i.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return categories
            .Select(cat => Evaluate(cat, asOf, breakEvenThreshold))
            .ToList()
            .AsReadOnly();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns how many days of utilization data exist for this category
    /// (earliest checkout for any asset in the category to asOf).
    /// </summary>
    private int GetUtilizationDataDays(string assetCategory, DateTime asOf)
    {
        var items = _equipmentService.GetAllItems()
            .Where(i => i.Category.Equals(assetCategory, StringComparison.OrdinalIgnoreCase))
            .ToList();

        DateTime? earliest = null;
        foreach (var item in items)
        {
            var history = _equipmentService.GetCheckoutHistory(item.Id);
            if (history.Any())
            {
                var first = history.Min(r => r.CheckedOutAtUtc);
                if (earliest is null || first < earliest)
                    earliest = first;
            }
        }

        if (earliest is null) return 0;
        return Math.Max(0, (int)(asOf - earliest.Value).TotalDays);
    }

    /// <summary>
    /// Average utilization rate (trailing 90 days) across all assets in a category.
    /// </summary>
    private double GetCategoryUtilization(string assetCategory, DateTime asOf)
    {
        var items = _equipmentService.GetAllItems()
            .Where(i => i.Category.Equals(assetCategory, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!items.Any()) return 0.0;

        var rates = items
            .Select(i => _utilizationService.GetAssetUtilization(i.Id, asOf))
            .Where(m => m is not null)
            .Select(m => m!.Trailing3MonthsRate)
            .ToList();

        return rates.Any() ? rates.Average() : 0.0;
    }
}
