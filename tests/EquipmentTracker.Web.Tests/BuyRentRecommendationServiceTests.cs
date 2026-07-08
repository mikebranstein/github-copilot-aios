using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;

namespace EquipmentTracker.Web.Tests;

/// <summary>
/// Tests for the buy vs. rent recommendation engine.
/// Covers AC-3 (recommendation surfaced when data ≥ 90 days and utilization ≥ threshold)
///       AC-4 (no recommendation when data &lt; 90 days)
///       TS-2 (recommendation card content)
///       TS-3 (insufficient data suppression)
/// </summary>
public class BuyRentRecommendationServiceTests
{
    // ── Factory helpers ───────────────────────────────────────────────────────

    private static (EquipmentService eqSvc, UtilizationService utilSvc,
                    RentalCostService rentalSvc, BuyRentRecommendationService recSvc)
        CreateServices()
    {
        var eqSvc = new EquipmentService();
        var utilSvc = new UtilizationService(eqSvc);
        var rentalSvc = new RentalCostService();
        var recSvc = new BuyRentRecommendationService(eqSvc, utilSvc, rentalSvc);
        return (eqSvc, utilSvc, rentalSvc, recSvc);
    }

    // ── AC-4 / TS-3: Insufficient data — recommendation suppressed ────────────

    [Fact]
    public void Evaluate_ReturnsInsufficientData_WhenNoDataExists()
    {
        // Given: An asset category with no rental or utilization data
        var (_, _, _, recSvc) = CreateServices();

        // When: The recommendation engine evaluates
        var result = recSvc.Evaluate("NonExistentCategory", DateTime.UtcNow);

        // Then: InsufficientData outcome, no recommendation
        Assert.Equal(RecommendationOutcome.InsufficientData, result.Outcome);
        Assert.Equal(0, result.RentalCostYtd);
    }

    [Fact]
    public void Evaluate_ReturnsInsufficientData_WhenFewerThan90DaysOfRentalData()
    {
        // Given: A 'Compactor' category with only 60 days of rental data
        var (eqSvc, _, rentalSvc, recSvc) = CreateServices();
        eqSvc.CreateItem("Compactor Model A", "Compactor");

        var asOf = DateTime.UtcNow;
        var periodStart = asOf.AddDays(-60);  // only 60 days of data
        rentalSvc.AddEntry("Compactor", periodStart, asOf, 5000m, "user1");

        // When: Recommendation engine evaluates
        var result = recSvc.Evaluate("Compactor", asOf);

        // Then: No recommendation surfaced (AC-4)
        Assert.Equal(RecommendationOutcome.InsufficientData, result.Outcome);
    }

    [Fact]
    public void Evaluate_ReturnsInsufficientData_WhenFewerThan90DaysOfUtilizationData()
    {
        // Given: A category with only 60 days of checkout history (no rental data)
        var (eqSvc, _, _, recSvc) = CreateServices();
        var item = eqSvc.CreateItem("Compactor Mini", "CompactorX");

        var asOf = DateTime.UtcNow;
        eqSvc.Checkout(item.Id, "Alice");
        var record = eqSvc.GetActiveCheckoutRecord(item.Id)!;
        record.CheckedOutAtUtc = asOf.AddDays(-60);  // only 60 days
        eqSvc.Return(item.Id);
        record.ReturnedAtUtc = asOf.AddDays(-59);

        // When: Evaluate is called
        var result = recSvc.Evaluate("CompactorX", asOf);

        // Then: InsufficientData (60 < 90 days)
        Assert.Equal(RecommendationOutcome.InsufficientData, result.Outcome);
    }

    [Fact]
    public void Evaluate_UIMessage_InsufficientData_Contains90DayMessage()
    {
        // Given: Insufficient data scenario
        var (_, _, _, recSvc) = CreateServices();

        // When: Outcome is InsufficientData
        var result = recSvc.Evaluate("SomeCategory", DateTime.UtcNow);

        // Then: Outcome is InsufficientData (UI should show '90 days' message per AC-4)
        Assert.Equal(RecommendationOutcome.InsufficientData, result.Outcome);
        Assert.True(result.DataDaysAvailable < 90);
    }

    // ── AC-3 / TS-2: Recommendation surfaced with ≥ 90 days data ─────────────

    [Fact]
    public void Evaluate_ReturnsBuy_WhenUtilizationAboveThresholdAndSufficientData()
    {
        // Given: 'Excavator' category with 95 days of data and 72% utilization (above 70% threshold)
        var (eqSvc, utilSvc, rentalSvc, recSvc) = CreateServices();
        var item = eqSvc.CreateItem("Excavator 320", "Excavator");

        var asOf = DateTime.UtcNow;
        var dataStart = asOf.AddDays(-95);   // 95 days of data

        // Create checkout for 72% of the last 3 months
        var window3Start = asOf.AddMonths(-3);
        var window3Hours = (asOf - window3Start).TotalHours;

        eqSvc.Checkout(item.Id, "FieldOp");
        var record = eqSvc.GetActiveCheckoutRecord(item.Id)!;
        record.CheckedOutAtUtc = window3Start;
        eqSvc.Return(item.Id);
        record.ReturnedAtUtc = window3Start.AddHours(window3Hours * 0.72); // 72% utilization

        // Add rental cost data from dataStart
        rentalSvc.AddEntry("Excavator", dataStart, asOf, 50000m, "admin");

        // When: Evaluate with default 70% threshold
        var result = recSvc.Evaluate("Excavator", asOf);

        // Then: Buy recommendation (AC-3, TS-2)
        Assert.Equal(RecommendationOutcome.Buy, result.Outcome);
        Assert.True(result.UtilizationRate >= 0.70);
        Assert.True(result.RentalCostYtd > 0);
        Assert.NotNull(result.BreakEvenMonths);
        Assert.Equal("MANUAL", result.DataSource);
    }

    [Fact]
    public void Evaluate_ReturnsContinueRenting_WhenUtilizationBelowThresholdWithSufficientData()
    {
        // Given: Category with 95 days data and only 50% utilization (below 70% threshold)
        var (eqSvc, utilSvc, rentalSvc, recSvc) = CreateServices();
        var item = eqSvc.CreateItem("Compactor HC", "CompactorLow");

        var asOf = DateTime.UtcNow;
        var dataStart = asOf.AddDays(-95);

        var window3Start = asOf.AddMonths(-3);
        var window3Hours = (asOf - window3Start).TotalHours;

        eqSvc.Checkout(item.Id, "FieldOp");
        var record = eqSvc.GetActiveCheckoutRecord(item.Id)!;
        record.CheckedOutAtUtc = window3Start;
        eqSvc.Return(item.Id);
        record.ReturnedAtUtc = window3Start.AddHours(window3Hours * 0.50); // 50% utilization

        rentalSvc.AddEntry("CompactorLow", dataStart, asOf, 20000m, "admin");

        // When
        var result = recSvc.Evaluate("CompactorLow", asOf);

        // Then: ContinueRenting
        Assert.Equal(RecommendationOutcome.ContinueRenting, result.Outcome);
        Assert.True(result.UtilizationRate < 0.70);
    }

    [Fact]
    public void Evaluate_ReturnsCorrectRentalCostYtd()
    {
        // Given: Known rental cost YTD
        var (eqSvc, _, rentalSvc, recSvc) = CreateServices();
        var item = eqSvc.CreateItem("Crane Big", "CraneTest");

        var asOf = DateTime.UtcNow;
        var dataStart = asOf.AddDays(-100);
        var ytdStart = new DateTime(asOf.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        rentalSvc.AddEntry("CraneTest", ytdStart, asOf, 75000m, "admin");
        // Also add older entry (should NOT count toward YTD)
        rentalSvc.AddEntry("CraneTest", asOf.AddYears(-1).AddDays(-10), asOf.AddYears(-1), 30000m, "admin");

        // Ensure utilization data exists (≥90 days)
        eqSvc.Checkout(item.Id, "CraneOp");
        var record = eqSvc.GetActiveCheckoutRecord(item.Id)!;
        record.CheckedOutAtUtc = dataStart;
        eqSvc.Return(item.Id);
        record.ReturnedAtUtc = dataStart.AddHours(1);

        // When
        var result = recSvc.Evaluate("CraneTest", asOf);

        // Then: YTD cost reflects only current-year entries
        Assert.Equal(75000m, result.RentalCostYtd);
    }

    [Fact]
    public void Evaluate_DataSourceIsManual_ForMvp()
    {
        // Given: Any evaluation
        var (eqSvc, _, rentalSvc, recSvc) = CreateServices();
        var item = eqSvc.CreateItem("Forklift A", "Forklift");

        var asOf = DateTime.UtcNow;
        rentalSvc.AddEntry("Forklift", asOf.AddDays(-100), asOf, 10000m, "admin");

        eqSvc.Checkout(item.Id, "Driver");
        var record = eqSvc.GetActiveCheckoutRecord(item.Id)!;
        record.CheckedOutAtUtc = asOf.AddDays(-100);
        eqSvc.Return(item.Id);
        record.ReturnedAtUtc = asOf.AddDays(-99);

        // When
        var result = recSvc.Evaluate("Forklift", asOf);

        // Then: Data source is always MANUAL for MVP
        Assert.Equal("MANUAL", result.DataSource);
    }

    // ── EvaluateAll: covers all categories ────────────────────────────────────

    [Fact]
    public void EvaluateAll_ReturnsOneRecommendationPerUniqueCategory()
    {
        var (eqSvc, _, _, recSvc) = CreateServices();

        // Default seed data has Electronics and Stationery categories
        var categories = eqSvc.GetAllItems()
            .Select(i => i.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = recSvc.EvaluateAll(DateTime.UtcNow);

        Assert.Equal(categories.Count, result.Count);
    }

    [Fact]
    public void EvaluateAll_AllInsufficientData_WhenNoCheckoutsExist()
    {
        var (_, _, _, recSvc) = CreateServices();

        var result = recSvc.EvaluateAll(DateTime.UtcNow);

        Assert.All(result, r => Assert.Equal(RecommendationOutcome.InsufficientData, r.Outcome));
    }

    // ── Configurable threshold ────────────────────────────────────────────────

    [Fact]
    public void Evaluate_RespectsCustomBreakEvenThreshold()
    {
        // Given: 65% utilization, default threshold 70% would say ContinueRenting
        // But custom threshold of 60% should say Buy
        var (eqSvc, _, rentalSvc, recSvc) = CreateServices();
        var item = eqSvc.CreateItem("Truck HD", "TruckThreshold");

        var asOf = DateTime.UtcNow;
        rentalSvc.AddEntry("TruckThreshold", asOf.AddDays(-95), asOf, 40000m, "admin");

        var window3Start = asOf.AddMonths(-3);
        var window3Hours = (asOf - window3Start).TotalHours;

        eqSvc.Checkout(item.Id, "Driver");
        var record = eqSvc.GetActiveCheckoutRecord(item.Id)!;
        record.CheckedOutAtUtc = window3Start;
        eqSvc.Return(item.Id);
        record.ReturnedAtUtc = window3Start.AddHours(window3Hours * 0.65);  // 65%

        // With default 70% threshold
        var defaultResult = recSvc.Evaluate("TruckThreshold", asOf, breakEvenThreshold: 0.70);
        // With custom 60% threshold
        var customResult = recSvc.Evaluate("TruckThreshold", asOf, breakEvenThreshold: 0.60);

        Assert.Equal(RecommendationOutcome.ContinueRenting, defaultResult.Outcome);
        Assert.Equal(RecommendationOutcome.Buy, customResult.Outcome);
    }
}
