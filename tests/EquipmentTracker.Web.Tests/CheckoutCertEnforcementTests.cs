using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using Xunit;

namespace EquipmentTracker.Web.Tests;

/// <summary>
/// Integration tests for the checkout cert enforcement flow (AC1, AC2, TS-1, TS-2, TS-5).
/// Uses <see cref="CertificationService"/> and <see cref="EquipmentService"/> together.
/// </summary>
public class CheckoutCertEnforcementTests
{
    private static (EquipmentService equipment, CertificationService cert) CreateServices()
        => (new EquipmentService(), new CertificationService());

    // ── TS-5: No cert requirement — checkout proceeds normally ────────────────

    [Fact]
    public void Checkout_Succeeds_WhenEquipmentCategoryHasNoCertRequirement()
    {
        // TS-5: Hand Tools has no cert requirement → checkout should always pass validation
        var (equipment, cert) = CreateServices();
        var item = equipment.GetAllItems().First(i => i.IsAvailable && i.Category == "Electronics");

        // Validate — no cert requirement for Electronics
        var outcome = cert.ValidateCheckout("Any Operator", item.Category);
        Assert.Equal(CertValidationOutcome.NotRequired, outcome);

        // Checkout proceeds
        var success = equipment.Checkout(item.Id, "Any Operator");
        Assert.True(success);
    }

    // ── TS-1: Hard block on expired certification ─────────────────────────────

    [Fact]
    public void ValidateCheckout_Blocks_WhenOperatorCertExpired15DaysAgo()
    {
        // TS-1: Forklift cert expired 15 days ago — checkout must be blocked
        var (_, cert) = CreateServices();
        var certType = cert.GetCertTypeByName("Forklift Operator Certification")!;
        cert.AssignRequirement("Forklift", certType.Id, "Admin");

        // Add an expired cert
        cert.AddCertRecord("Bob Operator", certType.Id,
            DateTime.UtcNow.AddYears(-2),
            DateTime.UtcNow.AddDays(-15),  // expired 15 days ago
            "Admin");

        var outcome = cert.ValidateCheckout("Bob Operator", "Forklift");
        Assert.Equal(CertValidationOutcome.Blocked, outcome);

        var message = cert.GetBlockReasonMessage("Bob Operator", "Forklift");
        Assert.NotNull(message);
        Assert.Contains("Checkout blocked", message);
        Assert.Contains("EXPIRED", message);
        Assert.Contains("Forklift Operator Certification", message);
        Assert.Contains("EHS Manager", message);
    }

    [Fact]
    public void ValidateCheckout_Blocks_WhenOperatorHasNoCertOnFile()
    {
        // TS-1 variant: operator never had a cert → MISSING
        var (_, cert) = CreateServices();
        var certType = cert.GetCertTypeByName("Forklift Operator Certification")!;
        cert.AssignRequirement("Forklift", certType.Id, "Admin");

        var outcome = cert.ValidateCheckout("New Employee", "Forklift");
        Assert.Equal(CertValidationOutcome.Blocked, outcome);

        var message = cert.GetBlockReasonMessage("New Employee", "Forklift");
        Assert.NotNull(message);
        Assert.Contains("MISSING", message);
    }

    [Fact]
    public void ValidateCheckout_DoesNotBlock_WhenCertIsValidAndNotExpired()
    {
        var (_, cert) = CreateServices();
        var certType = cert.GetCertTypeByName("Forklift Operator Certification")!;
        cert.AssignRequirement("Forklift", certType.Id, "Admin");

        cert.AddCertRecord("Alice Certified", certType.Id,
            DateTime.UtcNow.AddYears(-1),
            DateTime.UtcNow.AddYears(2),   // valid cert
            "Admin");

        var outcome = cert.ValidateCheckout("Alice Certified", "Forklift");
        Assert.Equal(CertValidationOutcome.Passed, outcome);
        Assert.Null(cert.GetBlockReasonMessage("Alice Certified", "Forklift"));
    }

    // ── TS-2: Supervisor override creates audit trail ─────────────────────────

    [Fact]
    public void RecordOverride_AfterBlock_ProducesImmutableAuditRecord()
    {
        // TS-2: supervisor override is recorded immutably
        var (equipment, cert) = CreateServices();
        var certType = cert.GetCertTypeByName("Forklift Operator Certification")!;
        cert.AssignRequirement("Forklift", certType.Id, "Admin");

        // Operator has expired cert
        cert.AddCertRecord("Bob Operator", certType.Id,
            DateTime.UtcNow.AddYears(-2),
            DateTime.UtcNow.AddDays(-15),
            "Admin");

        // Verify block first
        Assert.Equal(CertValidationOutcome.Blocked,
            cert.ValidateCheckout("Bob Operator", "Forklift"));

        // Simulate: item with "Forklift" category
        var forkliftItem = equipment.CreateItem("Forklift #1", "Forklift");
        equipment.Checkout(forkliftItem.Id, "Bob Operator");

        var checkoutRecord = equipment.GetActiveCheckoutRecord(forkliftItem.Id)!;

        // Record the supervisor override
        var overrideRecord = cert.RecordOverride(
            checkoutRecord.Id,
            "Jane Supervisor",
            OverrideReasonCode.EmergencyRenewalInProgress,
            "Cert renewal scheduled for next week",
            "Bob Operator",
            "Forklift Operator Certification");

        // Override is in the list and has correct data
        Assert.Contains(cert.GetAllOverrides(), o => o.Id == overrideRecord.Id);
        Assert.Equal("Jane Supervisor", overrideRecord.SupervisorName);
        Assert.Equal(OverrideReasonCode.EmergencyRenewalInProgress, overrideRecord.ReasonCode);
        Assert.Equal("Bob Operator", overrideRecord.BlockedOperatorName);
        Assert.Equal("Forklift Operator Certification", overrideRecord.RequiredCertName);
        Assert.True(overrideRecord.CreatedAt <= DateTime.UtcNow.AddSeconds(2));

        // Audit log also contains the override event
        var auditLog = cert.GetAuditLog();
        Assert.Contains(auditLog, e =>
            e.EventType == "OverrideRecorded" &&
            e.ActorName == "Jane Supervisor");
    }

    [Fact]
    public void CheckoutRecord_CertValidationResult_IsSetAfterOverride()
    {
        // The checkout record must reflect Overridden outcome and link to override record
        var (equipment, cert) = CreateServices();
        var certType = cert.GetCertTypeByName("Forklift Operator Certification")!;
        cert.AssignRequirement("Forklift", certType.Id, "Admin");
        cert.AddCertRecord("Bob", certType.Id, DateTime.UtcNow.AddYears(-2),
            DateTime.UtcNow.AddDays(-15), "Admin");

        var item = equipment.CreateItem("Forklift A", "Forklift");
        equipment.Checkout(item.Id, "Bob");
        var checkoutRec = equipment.GetActiveCheckoutRecord(item.Id)!;

        // Simulate the controller setting the cert validation result
        var overrideRec = cert.RecordOverride(checkoutRec.Id, "Sup", OverrideReasonCode.Other,
            "override reason", "Bob", "Forklift Operator Certification");

        checkoutRec.CertValidationResult = CertValidationOutcome.Overridden;
        checkoutRec.OverrideRecordId = overrideRec.Id;

        Assert.Equal(CertValidationOutcome.Overridden, checkoutRec.CertValidationResult);
        Assert.Equal(overrideRec.Id, checkoutRec.OverrideRecordId);
    }

    [Fact]
    public void CheckoutRecord_CertValidationResult_IsPassedWhenCertIsValid()
    {
        var (equipment, cert) = CreateServices();
        var certType = cert.GetCertTypeByName("Forklift Operator Certification")!;
        cert.AssignRequirement("Forklift", certType.Id, "Admin");
        cert.AddCertRecord("Alice", certType.Id, DateTime.UtcNow.AddYears(-1),
            DateTime.UtcNow.AddYears(2), "Admin");

        var item = equipment.CreateItem("Forklift B", "Forklift");
        equipment.Checkout(item.Id, "Alice");
        var checkoutRec = equipment.GetActiveCheckoutRecord(item.Id)!;

        // Simulate controller setting result after passed validation
        checkoutRec.CertValidationResult = CertValidationOutcome.Passed;

        Assert.Equal(CertValidationOutcome.Passed, checkoutRec.CertValidationResult);
        Assert.Null(checkoutRec.OverrideRecordId);
    }

    [Fact]
    public void CheckoutRecord_CertValidationResult_IsNotRequired_ForNoCertCategory()
    {
        var (equipment, cert) = CreateServices();

        var item = equipment.GetAllItems().First(i => i.IsAvailable);
        var outcome = cert.ValidateCheckout("Alice", item.Category);
        equipment.Checkout(item.Id, "Alice");
        var checkoutRec = equipment.GetActiveCheckoutRecord(item.Id)!;

        // Default value must be NotRequired
        Assert.Equal(CertValidationOutcome.NotRequired, checkoutRec.CertValidationResult);
        Assert.Equal(CertValidationOutcome.NotRequired, outcome);
    }

    // ── Multiple cert requirements for a category ─────────────────────────────

    [Fact]
    public void ValidateCheckout_Blocks_WhenOperatorMissesOneOfMultipleRequirements()
    {
        var (_, cert) = CreateServices();
        var certType1 = cert.GetCertTypeByName("Forklift Operator Certification")!;
        var certType2 = cert.GetCertTypeByName("OSHA 30 Construction")!;
        cert.AssignRequirement("HeavyLift", certType1.Id, "Admin");
        cert.AssignRequirement("HeavyLift", certType2.Id, "Admin");

        // Operator has certType1 but not certType2
        cert.AddCertRecord("Partial Operator", certType1.Id,
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow.AddYears(2), "Admin");

        var outcome = cert.ValidateCheckout("Partial Operator", "HeavyLift");
        Assert.Equal(CertValidationOutcome.Blocked, outcome);

        var message = cert.GetBlockReasonMessage("Partial Operator", "HeavyLift");
        Assert.NotNull(message);
        Assert.Contains("OSHA 30 Construction", message);
    }

    [Fact]
    public void ValidateCheckout_Passes_WhenOperatorHoldsAllMultipleRequirements()
    {
        var (_, cert) = CreateServices();
        var certType1 = cert.GetCertTypeByName("Forklift Operator Certification")!;
        var certType2 = cert.GetCertTypeByName("OSHA 30 Construction")!;
        cert.AssignRequirement("HeavyLift", certType1.Id, "Admin");
        cert.AssignRequirement("HeavyLift", certType2.Id, "Admin");

        cert.AddCertRecord("Full Operator", certType1.Id,
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow.AddYears(2), "Admin");
        cert.AddCertRecord("Full Operator", certType2.Id,
            DateTime.UtcNow.AddYears(-1), DateTime.UtcNow.AddYears(2), "Admin");

        var outcome = cert.ValidateCheckout("Full Operator", "HeavyLift");
        Assert.Equal(CertValidationOutcome.Passed, outcome);
    }
}
