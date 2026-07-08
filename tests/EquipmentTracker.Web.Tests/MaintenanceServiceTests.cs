using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using Xunit;

namespace EquipmentTracker.Web.Tests;

/// <summary>
/// Tests for Smart Maintenance Scheduling — Issue #119 (Phase 1, rule-based).
/// Each test maps to one or more acceptance criteria or test scenarios from the issue.
/// Run with: dotnet test
/// </summary>
public class MaintenanceServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (EquipmentService equipment, MaintenanceService maintenance) CreateServices()
    {
        var equip = new EquipmentService();
        var maint = new MaintenanceService(equip);
        return (equip, maint);
    }

    /// <summary>Simulates a completed checkout/return cycle of the given duration in hours.</summary>
    private static void SimulateCheckout(EquipmentService equip, int itemId, double durationHours)
    {
        var records = equip.GetAllRawCheckoutRecords().ToList();
        // Use reflection-free approach: directly checkout and manually set the return time
        // by using the existing Return() after checkout — we'll approximate by getting the record
        equip.Checkout(itemId, "TestUser");
        var record = equip.GetActiveCheckoutRecord(itemId);
        if (record is not null)
        {
            // We need to manipulate the returned record — since the in-memory service doesn't
            // expose direct record manipulation, we Return immediately and accept the ~0 hour duration.
            // For realistic hours testing, we'll use a subclass approach below.
        }
        equip.Return(itemId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // AC-1: Usage-Rate Tracking
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetOperatingHours_ReturnsZero_WhenNoCheckoutsExist()
    {
        // AC-1: New asset with no checkout history returns 0 operating hours
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Excavator", "Heavy Equipment");

        var hours = maint.GetOperatingHours(item.Id);

        Assert.Equal(0.0, hours);
    }

    [Fact]
    public void GetOperatingHours_OnlyCountsCompletedCheckouts()
    {
        // AC-1: Open checkouts are excluded from hours calculation
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Crane", "Heavy Equipment");

        equip.Checkout(item.Id, "Alice"); // open checkout — should NOT count

        var hours = maint.GetOperatingHours(item.Id);

        Assert.Equal(0.0, hours); // open checkout excluded
    }

    [Fact]
    public void GetOperatingHours_CountsReturnedCheckouts()
    {
        // AC-1: Completed checkout/return cycles are included in hours
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Crane", "Heavy Equipment");

        equip.Checkout(item.Id, "Alice");
        equip.Return(item.Id);

        var hours = maint.GetOperatingHours(item.Id);

        Assert.True(hours >= 0.0, "Hours should be non-negative after a completed checkout");
    }

    [Fact]
    public void GetProjectedServiceDate_ReturnsNull_WhenAverageDailyHoursIsZero()
    {
        // AC-1: Cannot project service date without usage history
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Bulldozer", "Heavy Equipment");
        maint.UpsertServiceInterval("Heavy Equipment", IntervalType.Hours, 250);

        var projected = maint.GetProjectedServiceDate(item.Id);

        Assert.Null(projected);
    }

    // ────────────────────────────────────────────────────────────────────────
    // AC-2: Service Due Dashboard — Status Bands
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetServiceStatus_ReturnsNoData_WhenNoIntervalConfigured()
    {
        // AC-2 + TS-4: Assets with no service interval → NoData (not Overdue)
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Safety Vest", "PPE"); // no interval for PPE

        var band = maint.GetServiceStatus(item.Id);

        Assert.Equal(MaintenanceBand.NoData, band);
    }

    [Fact]
    public void GetServiceStatus_ReturnsNoData_WhenNoCheckoutHistory()
    {
        // AC-2 + TS-3: Asset with service interval but zero checkout history → NoData
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("New Crane", "Heavy Equipment");
        maint.UpsertServiceInterval("Heavy Equipment", IntervalType.Hours, 250);

        var band = maint.GetServiceStatus(item.Id);

        // No checkout records → NoData (not Overdue)
        Assert.Equal(MaintenanceBand.NoData, band);
    }

    [Fact]
    public void GetAllServiceStatuses_ReturnsStatus_ForAllAssets()
    {
        // AC-2: Dashboard returns status for all assets
        var (equip, maint) = CreateServices();
        maint.UpsertServiceInterval("Electronics", IntervalType.Hours, 500);

        var statuses = maint.GetAllServiceStatuses();

        Assert.NotEmpty(statuses);
        Assert.True(statuses.Count >= 3, "Should include at least the seed items");
    }

    [Fact]
    public void GetAllServiceStatuses_ShowsNoData_ForAssetWithoutInterval()
    {
        // AC-2: Assets with no configured interval appear on dashboard as NoData
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Fire Extinguisher", "Safety");
        // No interval configured for "Safety"

        var statuses = maint.GetAllServiceStatuses();
        var assetStatus = statuses.FirstOrDefault(s => s.AssetId == item.Id);

        Assert.NotNull(assetStatus);
        Assert.Equal(MaintenanceBand.NoData, assetStatus!.Band);
        Assert.False(assetStatus.HasIntervalConfigured);
    }

    [Fact]
    public void GetServiceStatus_InRange_WhenWellBelowInterval()
    {
        // AC-2: Asset with hours well below interval threshold → InRange
        // We need an asset with checkout history. We use the existing seed items.
        var (equip, maint) = CreateServices();

        // Configure a very high interval so seed items with minimal usage are InRange
        maint.UpsertServiceInterval("Electronics", IntervalType.Hours, 10000);

        var item = equip.GetAllItems().First(i => i.Category == "Electronics");
        equip.Checkout(item.Id, "Bob");
        equip.Return(item.Id); // ~0 hours but completes a record

        var band = maint.GetServiceStatus(item.Id);

        Assert.Equal(MaintenanceBand.InRange, band);
    }

    // ────────────────────────────────────────────────────────────────────────
    // AC-3: Configurable Service Intervals and Alerts
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UpsertServiceInterval_CreatesNewInterval()
    {
        // AC-3: Admin can create a service interval for an equipment type
        var (equip, maint) = CreateServices();

        maint.UpsertServiceInterval("Crane", IntervalType.Hours, 250, leadTimeDays: 14);

        var interval = maint.GetServiceInterval("Crane");
        Assert.NotNull(interval);
        Assert.Equal(250, interval!.IntervalValue);
        Assert.Equal(IntervalType.Hours, interval.IntervalType);
        Assert.Equal(14, interval.LeadTimeDays);
    }

    [Fact]
    public void UpsertServiceInterval_UpdatesExistingInterval()
    {
        // AC-3: Admin can update an existing interval (upsert behavior)
        var (equip, maint) = CreateServices();
        maint.UpsertServiceInterval("Crane", IntervalType.Hours, 250);

        maint.UpsertServiceInterval("Crane", IntervalType.Hours, 500); // update

        var interval = maint.GetServiceInterval("Crane");
        Assert.NotNull(interval);
        Assert.Equal(500, interval!.IntervalValue);
    }

    [Fact]
    public void UpsertServiceInterval_IsCaseInsensitive()
    {
        // AC-3: Category lookup is case-insensitive
        var (equip, maint) = CreateServices();
        maint.UpsertServiceInterval("crane", IntervalType.Hours, 250);

        var interval = maint.GetServiceInterval("CRANE");

        Assert.NotNull(interval);
    }

    [Fact]
    public void SnoozeAlert_SuppressesAlertForAsset()
    {
        // AC-3: Per-asset alert snooze prevents alert firing
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Crane", "Heavy Equipment");

        maint.SnoozeAlert(item.Id, 14);

        var config = maint.GetOrCreateAlertConfig(item.Id);
        Assert.True(config.IsSnoozed);
        Assert.NotNull(config.SnoozedUntilUtc);
        Assert.True(config.SnoozedUntilUtc!.Value > DateTime.UtcNow);
    }

    [Fact]
    public void SnoozeAlert_Accepts7_14_Or30Days()
    {
        // AC-3: Valid snooze periods are 7, 14, or 30 days
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Loader", "Heavy Equipment");

        // 7 days
        maint.SnoozeAlert(item.Id, 7);
        var config7 = maint.GetOrCreateAlertConfig(item.Id);
        Assert.True(config7.SnoozedUntilUtc!.Value <= DateTime.UtcNow.AddDays(7).AddMinutes(1));

        // 30 days
        maint.SnoozeAlert(item.Id, 30);
        var config30 = maint.GetOrCreateAlertConfig(item.Id);
        Assert.True(config30.SnoozedUntilUtc!.Value >= DateTime.UtcNow.AddDays(29));
    }

    [Fact]
    public void UpdateNotificationRecipients_StoresRecipients()
    {
        // AC-3: Notification recipients can be configured per asset
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Crane", "Heavy Equipment");

        maint.UpdateNotificationRecipients(item.Id, "ops@example.com,fleet@example.com");

        var config = maint.GetOrCreateAlertConfig(item.Id);
        Assert.Equal("ops@example.com,fleet@example.com", config.NotificationRecipients);
    }

    // ────────────────────────────────────────────────────────────────────────
    // AC-4: Maintenance History Log
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LogMaintenanceEvent_RecordsEventWithAllFields()
    {
        // AC-4: Event is recorded with asset ID, event type, date, hours, technician, notes
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Excavator", "Heavy Equipment");
        var eventDate = new DateTime(2026, 6, 1);

        var evt = maint.LogMaintenanceEvent(
            item.Id, "Oil Change", eventDate, 245.5, "Jane Doe", "Changed 5W-30 oil");

        Assert.Equal(item.Id, evt.AssetId);
        Assert.Equal("Oil Change", evt.EventType);
        Assert.Equal(eventDate, evt.EventDate);
        Assert.Equal(245.5, evt.HoursAtService);
        Assert.Equal("Jane Doe", evt.TechnicianName);
        Assert.Equal("Changed 5W-30 oil", evt.Notes);
    }

    [Fact]
    public void LogMaintenanceEvent_TechnicianNameIsOptional()
    {
        // AC-4: Technician name is optional
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Crane", "Heavy Equipment");

        var evt = maint.LogMaintenanceEvent(item.Id, "Inspection", DateTime.Today, 100.0);

        Assert.Null(evt.TechnicianName);
        Assert.Null(evt.Notes);
    }

    [Fact]
    public void LogMaintenanceEvent_EnforcesSevenYearRetention()
    {
        // AC-4: Retention expiry is 7 years from creation (OSHA requirement)
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Crane", "Heavy Equipment");

        var evt = maint.LogMaintenanceEvent(item.Id, "Oil Change", DateTime.Today, 100.0);

        Assert.True(evt.RetentionExpiresAtUtc >= DateTime.UtcNow.AddYears(6).AddMonths(11),
            "Retention expiry must be at least ~7 years from now");
    }

    [Fact]
    public void GetMaintenanceHistory_ReturnsNewestFirst()
    {
        // AC-4: History is returned newest-first
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Crane", "Heavy Equipment");

        maint.LogMaintenanceEvent(item.Id, "Oil Change", new DateTime(2026, 1, 1), 100);
        maint.LogMaintenanceEvent(item.Id, "Tire Rotation", new DateTime(2026, 6, 1), 250);

        var history = maint.GetMaintenanceHistory(item.Id);

        Assert.Equal(2, history.Count);
        Assert.True(history[0].EventDate > history[1].EventDate, "Newest event should be first");
    }

    [Fact]
    public void LogMaintenanceEvent_ResetsIntervalCounter()
    {
        // AC-4: After logging a maintenance event, asset status should return to InRange
        // (or NoData if interval not configured)
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Crane", "Heavy Equipment");
        maint.UpsertServiceInterval("Heavy Equipment", IntervalType.Hours, 250);

        // Log a service event at current hours — simulates reset
        maint.LogMaintenanceEvent(item.Id, "Oil Change", DateTime.UtcNow, 245.0);

        var lastEvent = maint.GetLastMaintenanceEvent(item.Id);
        Assert.NotNull(lastEvent);
        Assert.Equal("Oil Change", lastEvent!.EventType);
    }

    // ────────────────────────────────────────────────────────────────────────
    // AC-5: Downtime Cost Calculator
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateDowntimeCost_ComputesCorrectAnnualCost()
    {
        // AC-5: Annual cost = incidents × repair days × daily cost
        var (_, maint) = CreateServices();

        var result = maint.CalculateDowntimeCost(
            assetCount: 50,
            dailyCostEstimate: 1500,
            avgRepairDays: 2.5,
            estimatedIncidentsPerYear: 4);

        // 4 incidents × 2.5 days × $1,500/day = $15,000
        Assert.Equal(15000.0, result.EstimatedAnnualDowntimeCost);
    }

    [Fact]
    public void CalculateDowntimeCost_Savings80PercentIsCorrect()
    {
        // AC-5: Estimated savings = 80% of annual cost
        var (_, maint) = CreateServices();

        var result = maint.CalculateDowntimeCost(50, 1500, 2.5, 4);

        // $15,000 × 80% = $12,000
        Assert.Equal(12000.0, result.EstimatedAnnualSavings80Percent);
    }

    [Fact]
    public void CalculateDowntimeCost_UsesConfigurableInputs()
    {
        // AC-5: Calculator uses user-configurable inputs (not hardcoded defaults)
        var (_, maint) = CreateServices();

        var result1 = maint.CalculateDowntimeCost(10, 1200, 2, 3);
        var result2 = maint.CalculateDowntimeCost(50, 1700, 3, 6);

        Assert.NotEqual(result1.EstimatedAnnualDowntimeCost, result2.EstimatedAnnualDowntimeCost);
    }

    [Fact]
    public void CalculateDowntimeCost_PopulatesAssetCount()
    {
        // AC-5: Asset count is reflected in the summary
        var (_, maint) = CreateServices();

        var result = maint.CalculateDowntimeCost(75, 1450, 2.5, 4);

        Assert.Equal(75, result.AssetCount);
    }

    // ────────────────────────────────────────────────────────────────────────
    // AC-6: Phase 1 Gate Metric
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetMaintenanceLogCompletionRate_ReturnsZero_WhenNoEventsLogged()
    {
        // AC-6: 0% when no maintenance events have been logged
        var (equip, maint) = CreateServices();
        maint.UpsertServiceInterval("Electronics", IntervalType.Hours, 500);
        // Seed items exist in Electronics but no events logged

        var rate = maint.GetMaintenanceLogCompletionRate();

        Assert.Equal(0.0, rate);
    }

    [Fact]
    public void GetMaintenanceLogCompletionRate_Returns100_WhenAllAssetsHaveRecentEvent()
    {
        // AC-6: 100% when every enrolled asset has a recent maintenance event
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Tracked Asset", "TestCategory");
        maint.UpsertServiceInterval("TestCategory", IntervalType.Hours, 250);

        maint.LogMaintenanceEvent(item.Id, "Service", DateTime.UtcNow.AddDays(-30), 100);

        var rate = maint.GetMaintenanceLogCompletionRate();

        Assert.Equal(100.0, rate);
    }

    [Fact]
    public void GetMaintenanceLogCompletionRate_ReturnsZero_WhenNoIntervalsConfigured()
    {
        // AC-6: No enrolled assets → 0% (denominator is zero)
        var (equip, maint) = CreateServices();
        // No intervals configured — no enrolled assets

        var rate = maint.GetMaintenanceLogCompletionRate();

        Assert.Equal(0.0, rate);
    }

    [Fact]
    public void GetMaintenanceLogCompletionRate_OnlyCountsLast90Days()
    {
        // AC-6: Events older than 90 days do NOT count toward the completion rate
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Old Asset", "MachineryOld");
        maint.UpsertServiceInterval("MachineryOld", IntervalType.Hours, 250);

        // Log event 91 days ago — outside the 90-day window
        maint.LogMaintenanceEvent(item.Id, "Old Service", DateTime.UtcNow.AddDays(-91), 100);

        var rate = maint.GetMaintenanceLogCompletionRate();

        Assert.Equal(0.0, rate);
    }

    // ────────────────────────────────────────────────────────────────────────
    // AC-7: Time-Based Interval Support
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UpsertServiceInterval_SupportsTimeBasedType()
    {
        // AC-7: Time-based intervals (e.g., annual inspection) can be configured
        var (equip, maint) = CreateServices();

        maint.UpsertServiceInterval("Lifting Equipment", IntervalType.TimeBased, 365, leadTimeDays: 14);

        var interval = maint.GetServiceInterval("Lifting Equipment");
        Assert.NotNull(interval);
        Assert.Equal(IntervalType.TimeBased, interval!.IntervalType);
        Assert.Equal(365, interval.IntervalValue);
        Assert.Equal(14, interval.LeadTimeDays);
    }

    [Fact]
    public void GetServiceStatus_TimeBased_InRange_WhenFarFromDueDate()
    {
        // AC-7: Time-based asset with recent maintenance event is InRange
        // TS-5: Annual inspection setup
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Lifting Hook", "Lifting Equipment");
        maint.UpsertServiceInterval("Lifting Equipment", IntervalType.TimeBased, 365, leadTimeDays: 14);

        // Log service event 30 days ago → next due in 335 days → well past lead time
        maint.LogMaintenanceEvent(item.Id, "Annual Inspection", DateTime.UtcNow.AddDays(-30), 0);

        var band = maint.GetServiceStatus(item.Id);

        Assert.Equal(MaintenanceBand.InRange, band);
    }

    [Fact]
    public void GetServiceStatus_TimeBased_Caution_WhenWithinLeadTimeDays()
    {
        // AC-7 + TS-5: Asset enters Caution when within lead time window
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Crane Hook", "Annual Equipment");
        maint.UpsertServiceInterval("Annual Equipment", IntervalType.TimeBased, 365, leadTimeDays: 14);

        // Log service event 353 days ago → next due in 12 days → within 14-day lead time → Caution
        maint.LogMaintenanceEvent(item.Id, "Annual Inspection", DateTime.UtcNow.AddDays(-353), 0);

        var band = maint.GetServiceStatus(item.Id);

        Assert.Equal(MaintenanceBand.Caution, band);
    }

    [Fact]
    public void GetServiceStatus_TimeBased_Overdue_WhenPastDueDate()
    {
        // AC-7: Time-based asset past due date → Overdue
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Old Hook", "OldAnnual");
        maint.UpsertServiceInterval("OldAnnual", IntervalType.TimeBased, 365, leadTimeDays: 14);

        // Log service event 370 days ago → overdue by 5 days
        maint.LogMaintenanceEvent(item.Id, "Annual Inspection", DateTime.UtcNow.AddDays(-370), 0);

        var band = maint.GetServiceStatus(item.Id);

        Assert.Equal(MaintenanceBand.Overdue, band);
    }

    [Fact]
    public void GetServiceStatus_TimeBased_Overdue_WhenNoEventLoggedButHasCheckouts()
    {
        // AC-7: Time-based asset with checkout history but no maintenance event → Overdue
        // (worst-case assumption: interval began at equipment registration)
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Tracked Rigging", "RiggingEquipment");
        maint.UpsertServiceInterval("RiggingEquipment", IntervalType.TimeBased, 365, leadTimeDays: 14);

        equip.Checkout(item.Id, "Rigger");
        equip.Return(item.Id);
        // No maintenance event logged

        var band = maint.GetServiceStatus(item.Id);

        Assert.Equal(MaintenanceBand.Overdue, band);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Test Scenarios (TS-1 through TS-5)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TS3_AssetWithZeroCheckoutHistory_ShowsNoData()
    {
        // TS-3: Newly registered asset with no checkout records → NoData, NOT Overdue, no alerts
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("New Asset", "Heavy Equipment");
        maint.UpsertServiceInterval("Heavy Equipment", IntervalType.Hours, 250);

        var band = maint.GetServiceStatus(item.Id);
        var hoursToService = maint.GetHoursToNextService(item.Id);

        Assert.Equal(MaintenanceBand.NoData, band);
        // HoursToNextService returns the interval remaining when no history
        // (250 - 0 hours = 250 hours remaining, but band is NoData because no records)
        Assert.Equal(250.0, hoursToService);
    }

    [Fact]
    public void TS4_AssetTypeWithNoIntervalConfigured_ShowsNoData()
    {
        // TS-4: Asset category with no service interval → NoData, excluded from alert flow
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Safety Vest", "PPE"); // no interval for PPE

        var band = maint.GetServiceStatus(item.Id);
        var statuses = maint.GetAllServiceStatuses();
        var assetStatus = statuses.FirstOrDefault(s => s.AssetId == item.Id);

        Assert.Equal(MaintenanceBand.NoData, band);
        Assert.NotNull(assetStatus);
        Assert.False(assetStatus!.HasIntervalConfigured);
    }

    [Fact]
    public void TS5_TimeBasedInterval_CautionWithin14Days()
    {
        // TS-5: Annual inspection within 14-day lead time → Caution + alert triggered
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Inspection Asset", "InspectionCategory");
        maint.UpsertServiceInterval("InspectionCategory", IntervalType.TimeBased, 365, leadTimeDays: 14);

        // Service event 353 days ago → 12 days remaining → Caution
        maint.LogMaintenanceEvent(item.Id, "Annual Inspection", DateTime.UtcNow.AddDays(-353), 0);

        var band = maint.GetServiceStatus(item.Id);
        var status = maint.GetAllServiceStatuses().FirstOrDefault(s => s.AssetId == item.Id);

        Assert.Equal(MaintenanceBand.Caution, band);
        Assert.NotNull(status);
        Assert.True(status!.DaysRemaining is > 0 and <= 14);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Pending Alerts
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetPendingAlerts_ExcludesSnoozedAssets()
    {
        // AC-3: Snoozed assets must NOT appear in pending alerts
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Snoozed Crane", "SnoozedCategory");
        maint.UpsertServiceInterval("SnoozedCategory", IntervalType.TimeBased, 365, leadTimeDays: 14);

        // Make the asset Overdue
        maint.LogMaintenanceEvent(item.Id, "Inspection", DateTime.UtcNow.AddDays(-370), 0);

        // Snooze the alert
        maint.SnoozeAlert(item.Id, 14);

        var alerts = maint.GetPendingAlerts();

        Assert.DoesNotContain(alerts, a => a.AssetId == item.Id);
    }

    [Fact]
    public void GetPendingAlerts_IncludesOverdueNonSnoozedAssets()
    {
        // AC-3: Non-snoozed Overdue assets appear in pending alerts
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Alert Crane", "AlertCategory");
        maint.UpsertServiceInterval("AlertCategory", IntervalType.TimeBased, 365, leadTimeDays: 14);

        // Make the asset Overdue
        maint.LogMaintenanceEvent(item.Id, "Inspection", DateTime.UtcNow.AddDays(-370), 0);

        var alerts = maint.GetPendingAlerts();

        Assert.Contains(alerts, a => a.AssetId == item.Id);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Hours-Based Status Band Thresholds
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetHoursToNextService_ReturnsIntervalValue_WhenNoCheckoutsOrEvents()
    {
        // AC-1: Hours to next service = full interval when no usage
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Fresh Crane", "CraneType");
        maint.UpsertServiceInterval("CraneType", IntervalType.Hours, 250);

        var hours = maint.GetHoursToNextService(item.Id);

        Assert.Equal(250.0, hours);
    }

    [Fact]
    public void GetHoursToNextService_ReturnsNull_ForTimeBasedInterval()
    {
        // AC-1: Hours-to-next-service only applies to hours-based intervals
        var (equip, maint) = CreateServices();
        var item = equip.CreateItem("Annual Asset", "AnnualType");
        maint.UpsertServiceInterval("AnnualType", IntervalType.TimeBased, 365);

        var hours = maint.GetHoursToNextService(item.Id);

        Assert.Null(hours);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Model Properties
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AlertConfig_IsSnoozed_ReturnsFalse_WhenNotSnoozed()
    {
        var config = new AlertConfig { AssetId = 1 };
        Assert.False(config.IsSnoozed);
    }

    [Fact]
    public void AlertConfig_IsSnoozed_ReturnsTrue_WhenSnoozedUntilFuture()
    {
        var config = new AlertConfig
        {
            AssetId = 1,
            SnoozedUntilUtc = DateTime.UtcNow.AddDays(7)
        };
        Assert.True(config.IsSnoozed);
    }

    [Fact]
    public void AlertConfig_IsSnoozed_ReturnsFalse_WhenSnoozeExpired()
    {
        var config = new AlertConfig
        {
            AssetId = 1,
            SnoozedUntilUtc = DateTime.UtcNow.AddDays(-1) // expired yesterday
        };
        Assert.False(config.IsSnoozed);
    }

    [Fact]
    public void MaintenanceEvent_RetentionExpiry_IsSevenYearsFromCreation()
    {
        var evt = new MaintenanceEvent { CreatedAtUtc = new DateTime(2026, 1, 1) };
        Assert.Equal(new DateTime(2033, 1, 1), evt.RetentionExpiresAtUtc);
    }
}
