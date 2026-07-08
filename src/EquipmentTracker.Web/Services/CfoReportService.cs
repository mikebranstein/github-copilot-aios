using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-memory implementation of ICfoReportService.
/// Assembles report data from utilization, rental cost, and recommendation services.
/// The view layer renders this as print-optimized HTML (MVP; PDF library selection deferred per design risk R1).
/// </summary>
public class CfoReportService : ICfoReportService
{
    private readonly IEquipmentService _equipmentService;
    private readonly IUtilizationService _utilizationService;
    private readonly IBuyRentRecommendationService _recommendationService;

    // Idle threshold below which an asset contributes to estimated budget waste (< 40%).
    private const double IdleThreshold = 0.40;
    // Assumed average daily ownership cost (placeholder for MVP — no TCO data yet).
    private const decimal DailyOwnershipCostPlaceholder = 50m;

    public CfoReportService(
        IEquipmentService equipmentService,
        IUtilizationService utilizationService,
        IBuyRentRecommendationService recommendationService)
    {
        _equipmentService = equipmentService;
        _utilizationService = utilizationService;
        _recommendationService = recommendationService;
    }

    public CfoReportData GenerateReport(
        DateTime asOf,
        string companyName = "Your Company",
        DateTime? periodStart = null,
        DateTime? periodEnd = null,
        double breakEvenThreshold = 0.70)
    {
        var end = periodEnd ?? asOf;
        var start = periodStart ?? asOf.AddMonths(-12);

        var allItems = _equipmentService.GetAllItems();

        if (!allItems.Any())
        {
            return new CfoReportData
            {
                CompanyName = companyName,
                ReportDate = asOf,
                PeriodStart = start,
                PeriodEnd = end,
                IsEmptyState = true
            };
        }

        // Section (a) — Fleet utilization summary
        var fleetMetrics = _utilizationService.GetFleetUtilization(asOf);

        // Section (b) — Estimated annual budget waste (idle assets <40 % utilization)
        var budgetWaste = EstimateBudgetWaste(fleetMetrics, start, end);

        // Section (c) — Top 3 recommendations
        var allRecs = _recommendationService.EvaluateAll(asOf, breakEvenThreshold);
        var top3 = allRecs
            .Where(r => r.Outcome != RecommendationOutcome.InsufficientData)
            .OrderByDescending(r => r.RentalCostYtd)
            .Take(3)
            .Select(r => new BuyRentRecommendationData
            {
                AssetCategory = r.AssetCategory,
                RecommendationText = BuildRecommendationText(r),
                RentalCostYtd = r.RentalCostYtd,
                UtilizationRate = r.UtilizationRate,
                BreakEvenMonths = r.BreakEvenMonths,
                DataSource = r.DataSource
            })
            .ToList()
            .AsReadOnly();

        // Section (d) — Month-over-month trend (trailing 12 months, one point per month)
        var monthlyTrend = BuildMonthlyTrend(asOf, 12);

        return new CfoReportData
        {
            FleetUtilizationSummary = fleetMetrics,
            EstimatedAnnualBudgetWaste = budgetWaste,
            TopRecommendations = top3,
            MonthlyTrend = monthlyTrend,
            CompanyName = companyName,
            ReportDate = asOf,
            PeriodStart = start,
            PeriodEnd = end,
            IsEmptyState = false
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static decimal EstimateBudgetWaste(
        IReadOnlyList<AssetUtilizationMetrics> metrics,
        DateTime periodStart,
        DateTime periodEnd)
    {
        var periodDays = (decimal)(periodEnd - periodStart).TotalDays;
        return metrics
            .Where(m => m.Trailing12MonthsRate < IdleThreshold)
            .Sum(m =>
            {
                // Waste = idle fraction of owned time * daily ownership cost
                var idleFraction = (decimal)(1.0 - m.Trailing12MonthsRate);
                return idleFraction * DailyOwnershipCostPlaceholder * periodDays / 365m;
            });
    }

    private static string BuildRecommendationText(BuyRentRecommendation r) =>
        r.Outcome == RecommendationOutcome.Buy
            ? $"You have spent ${r.RentalCostYtd:N0} renting {r.AssetCategory} this year. " +
              $"At {r.UtilizationRate:P0} utilization, break-even to own is " +
              $"{r.BreakEvenMonths:F0} months. Recommendation: Buy."
            : $"At {r.UtilizationRate:P0} utilization, continuing to rent {r.AssetCategory} is more cost-effective. " +
              $"Recommendation: Continue Renting.";

    private IReadOnlyList<MonthlyTrendPoint> BuildMonthlyTrend(DateTime asOf, int months)
    {
        var points = new List<MonthlyTrendPoint>();
        for (int i = months - 1; i >= 0; i--)
        {
            var monthEnd = new DateTime(asOf.Year, asOf.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddMonths(1 - i)
                .AddSeconds(-1);

            var fleet = _utilizationService.GetFleetUtilization(monthEnd);
            double avg = fleet.Any() ? fleet.Average(m => m.CurrentMonthRate) : 0.0;

            points.Add(new MonthlyTrendPoint
            {
                MonthLabel = monthEnd.ToString("MMM yyyy"),
                AverageFleetUtilizationRate = avg
            });
        }
        return points.AsReadOnly();
    }
}
