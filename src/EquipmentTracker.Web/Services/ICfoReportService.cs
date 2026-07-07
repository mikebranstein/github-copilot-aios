namespace EquipmentTracker.Web.Services;

/// <summary>
/// Data transfer object for the CFO Executive Report.
/// Contains all 5 required sections per AC-5.
/// </summary>
public class CfoReportData
{
    /// <summary>Section (a): Fleet utilization summary with per-asset breakdown.</summary>
    public IReadOnlyList<AssetUtilizationMetrics> FleetUtilizationSummary { get; init; } = [];

    /// <summary>Section (b): Estimated annual budget waste from underutilized owned assets.</summary>
    public decimal EstimatedAnnualBudgetWaste { get; init; }

    /// <summary>Section (c): Top 3 buy vs. rent recommendations with supporting data.</summary>
    public IReadOnlyList<BuyRentRecommendationData> TopRecommendations { get; init; } = [];

    /// <summary>Section (d): Month-over-month utilization trend data points.</summary>
    public IReadOnlyList<MonthlyTrendPoint> MonthlyTrend { get; init; } = [];

    /// <summary>Section (e): Report metadata.</summary>
    public string CompanyName { get; init; } = "Your Company";
    public DateTime ReportDate { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }

    /// <summary>Whether the report was generated from data (false = empty-state / no assets).</summary>
    public bool IsEmptyState { get; init; }
}

public class BuyRentRecommendationData
{
    public string AssetCategory { get; init; } = string.Empty;
    public string RecommendationText { get; init; } = string.Empty;
    public decimal RentalCostYtd { get; init; }
    public double UtilizationRate { get; init; }
    public double? BreakEvenMonths { get; init; }
    public string DataSource { get; init; } = "MANUAL";
}

public class MonthlyTrendPoint
{
    public string MonthLabel { get; init; } = string.Empty;
    public double AverageFleetUtilizationRate { get; init; }
}

/// <summary>
/// Service that assembles data for the CFO Executive Report.
/// PDF rendering is handled by the view layer (print-optimized HTML for MVP).
/// </summary>
public interface ICfoReportService
{
    /// <summary>
    /// Assembles all report data for the specified period.
    /// Uses the trailing 12 months as the reporting period when periodStart/End are not specified.
    /// </summary>
    CfoReportData GenerateReport(DateTime asOf, string companyName = "Your Company",
        DateTime? periodStart = null, DateTime? periodEnd = null,
        double breakEvenThreshold = 0.70);
}
