using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;

namespace EquipmentTracker.Web.Tests;

/// <summary>
/// Tests for CFO report access control and report generation.
/// Covers AC-5 (report generation with 5 required sections),
///       AC-6 (empty-state handling),
///       AC-7 (RBAC: FleetManager cannot export, CFO/Admin can).
///       TS-4 (CFO report generation and role-based access)
///       TS-5 (empty fleet — no assets tracked)
/// </summary>
public class CfoReportAccessTests
{
    // ── Factory helpers ───────────────────────────────────────────────────────

    private static (EquipmentService eqSvc, UtilizationService utilSvc,
                    RentalCostService rentalSvc, BuyRentRecommendationService recSvc,
                    CfoReportService reportSvc)
        CreateServices()
    {
        var eqSvc     = new EquipmentService();
        var utilSvc   = new UtilizationService(eqSvc);
        var rentalSvc = new RentalCostService();
        var recSvc    = new BuyRentRecommendationService(eqSvc, utilSvc, rentalSvc);
        var reportSvc = new CfoReportService(eqSvc, utilSvc, recSvc);
        return (eqSvc, utilSvc, rentalSvc, recSvc, reportSvc);
    }

    // ── AC-7 / TS-4: Role-based access control ────────────────────────────────

    [Fact]
    public void ApplicationUser_CanExportCfoReport_TrueForAdminRole()
    {
        // Given: A user with Admin role
        var user = new ApplicationUser { Username = "AdminUser", Role = UserRole.Admin };

        // Then: Export is allowed
        Assert.True(user.CanExportCfoReport);
    }

    [Fact]
    public void ApplicationUser_CanExportCfoReport_TrueForCfoRole()
    {
        // Given: A user with CFO role
        var user = new ApplicationUser { Username = "CfoUser", Role = UserRole.CFO };

        // Then: Export is allowed
        Assert.True(user.CanExportCfoReport);
    }

    [Fact]
    public void ApplicationUser_CanExportCfoReport_FalseForFleetManagerRole()
    {
        // Given: A user with FleetManager role (AC-7: Fleet Manager cannot export)
        var user = new ApplicationUser { Username = "FleetMgr", Role = UserRole.FleetManager };

        // Then: Export is NOT allowed
        Assert.False(user.CanExportCfoReport);
    }

    [Fact]
    public void ApplicationUser_CanExportCfoReport_FalseForStandardRole()
    {
        var user = new ApplicationUser { Username = "StandardUser", Role = UserRole.Standard };
        Assert.False(user.CanExportCfoReport);
    }

    [Fact]
    public void ApplicationUser_CanExportCfoReport_FalseForOperationsDirectorRole()
    {
        var user = new ApplicationUser { Username = "OpsDirector", Role = UserRole.OperationsDirector };
        Assert.False(user.CanExportCfoReport);
    }

    [Fact]
    public void ApplicationUser_CanViewUtilizationDashboard_TrueForFleetManager()
    {
        // Given: Fleet Manager should be able to VIEW the dashboard (just not export)
        var user = new ApplicationUser { Username = "FleetMgr", Role = UserRole.FleetManager };
        Assert.True(user.CanViewUtilizationDashboard);
    }

    [Fact]
    public void ApplicationUser_CanViewUtilizationDashboard_TrueForAdmin()
    {
        var user = new ApplicationUser { Username = "Admin", Role = UserRole.Admin };
        Assert.True(user.CanViewUtilizationDashboard);
    }

    [Fact]
    public void ApplicationUser_CanViewUtilizationDashboard_FalseForStandardUser()
    {
        var user = new ApplicationUser { Username = "Basic", Role = UserRole.Standard };
        Assert.False(user.CanViewUtilizationDashboard);
    }

    // ── AC-5 / TS-4: CFO report generation — 5 required sections ─────────────

    [Fact]
    public void GenerateReport_ReturnsCfoReportData_WithAllSections()
    {
        // Given: An authenticated Admin user with fleet data
        var (_, _, _, _, reportSvc) = CreateServices();

        // When: Report is generated
        var report = reportSvc.GenerateReport(DateTime.UtcNow, "Acme Corp");

        // Then: Report contains all 5 required sections (AC-5)
        // (a) Fleet utilization summary
        Assert.NotNull(report.FleetUtilizationSummary);
        // (b) Budget waste estimate
        Assert.True(report.EstimatedAnnualBudgetWaste >= 0);
        // (c) Top 3 recommendations (list exists, may be empty if insufficient data)
        Assert.NotNull(report.TopRecommendations);
        // (d) Monthly trend
        Assert.NotNull(report.MonthlyTrend);
        Assert.True(report.MonthlyTrend.Count > 0, "Monthly trend should have data points");
        // (e) Metadata
        Assert.Equal("Acme Corp", report.CompanyName);
        Assert.True(report.ReportDate > DateTime.MinValue);
        Assert.True(report.PeriodStart < report.PeriodEnd);
    }

    [Fact]
    public void GenerateReport_CompanyNameIsPreserved()
    {
        var (_, _, _, _, reportSvc) = CreateServices();

        var report = reportSvc.GenerateReport(DateTime.UtcNow, "BuildRight Construction");

        Assert.Equal("BuildRight Construction", report.CompanyName);
    }

    [Fact]
    public void GenerateReport_ReportDate_EqualsAsOf()
    {
        var (_, _, _, _, reportSvc) = CreateServices();
        var asOf = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        var report = reportSvc.GenerateReport(asOf);

        Assert.Equal(asOf, report.ReportDate);
    }

    [Fact]
    public void GenerateReport_PeriodDefaults_ToTrailing12Months()
    {
        var (_, _, _, _, reportSvc) = CreateServices();
        var asOf = DateTime.UtcNow;

        var report = reportSvc.GenerateReport(asOf);

        // Default period should be asOf - 12 months to asOf
        Assert.Equal(asOf, report.PeriodEnd);
        Assert.True((asOf - report.PeriodStart).TotalDays >= 360);  // ~12 months
    }

    [Fact]
    public void GenerateReport_MonthlyTrend_Contains12DataPoints()
    {
        var (_, _, _, _, reportSvc) = CreateServices();

        var report = reportSvc.GenerateReport(DateTime.UtcNow);

        // Monthly trend should have 12 data points (one per month, trailing 12 months)
        Assert.Equal(12, report.MonthlyTrend.Count);
    }

    [Fact]
    public void GenerateReport_FleetSummary_MatchesAllTrackedAssets()
    {
        var (eqSvc, _, _, _, reportSvc) = CreateServices();

        var report = reportSvc.GenerateReport(DateTime.UtcNow);

        // Fleet summary should include all tracked assets
        Assert.Equal(eqSvc.GetAllItems().Count, report.FleetUtilizationSummary.Count);
    }

    // ── TS-5: Empty fleet — empty state handling ──────────────────────────────

    [Fact]
    public void GenerateReport_IsEmptyState_False_WhenAssetsExist()
    {
        // Given: Fleet has assets (seed data)
        var (_, _, _, _, reportSvc) = CreateServices();

        var report = reportSvc.GenerateReport(DateTime.UtcNow);

        // Then: Not an empty state
        Assert.False(report.IsEmptyState);
    }

    [Fact]
    public void GenerateReport_TopRecommendations_LimitedToThree()
    {
        // Given: Multiple categories with sufficient data
        var (eqSvc, _, rentalSvc, _, reportSvc) = CreateServices();

        var asOf = DateTime.UtcNow;
        // Add 5 categories with 95 days of rental data
        for (int i = 1; i <= 5; i++)
        {
            var cat = $"Category{i}";
            var item = eqSvc.CreateItem($"Asset {i}", cat);
            rentalSvc.AddEntry(cat, asOf.AddDays(-95), asOf, 1000m * i, "admin");

            // Add utilization data
            eqSvc.Checkout(item.Id, "Worker");
            var record = eqSvc.GetActiveCheckoutRecord(item.Id)!;
            record.CheckedOutAtUtc = asOf.AddDays(-95);
            eqSvc.Return(item.Id);
            record.ReturnedAtUtc = asOf.AddDays(-94);
        }

        var report = reportSvc.GenerateReport(asOf);

        // Then: Top recommendations are capped at 3
        Assert.True(report.TopRecommendations.Count <= 3,
            "CFO report should show at most 3 buy/rent recommendations");
    }

    // ── RentalCostService ─────────────────────────────────────────────────────

    [Fact]
    public void RentalCostService_GetYtdCost_SumsCurrentYearEntriesOnly()
    {
        var svc  = new RentalCostService();
        var asOf = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        // Current year entry
        svc.AddEntry("Excavator", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc), 15000m, "user1");

        // Previous year entry (should NOT count toward YTD)
        svc.AddEntry("Excavator", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc), 40000m, "user1");

        var ytd = svc.GetYtdCost("Excavator", asOf);

        Assert.Equal(15000m, ytd);
    }

    [Fact]
    public void RentalCostService_GetDataDaysAvailable_ReturnsZeroWhenNoEntries()
    {
        var svc = new RentalCostService();

        int days = svc.GetDataDaysAvailable("NonExistent", DateTime.UtcNow);

        Assert.Equal(0, days);
    }

    [Fact]
    public void RentalCostService_AddEntry_StoresCostCorrectly()
    {
        var svc   = new RentalCostService();
        var start = DateTime.UtcNow.AddMonths(-3);
        var end   = DateTime.UtcNow;

        var entry = svc.AddEntry("Loader", start, end, 12500m, "admin");

        Assert.Equal(12500m, entry.CostAmount);
        Assert.Equal("Loader", entry.AssetCategory);
        Assert.Equal("MANUAL", entry.EntrySource);
        Assert.Equal("admin", entry.EnteredBy);
    }
}
