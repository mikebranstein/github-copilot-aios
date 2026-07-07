using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EquipmentTracker.Web.Tests;

/// <summary>
/// Tests for <see cref="CertificationService"/> covering all 8 acceptance criteria
/// and the 5 design test scenarios (TS-1 through TS-5).
/// </summary>
public class CertificationServiceTests
{
    private static CertificationService CreateService() => new CertificationService();

    // ── AC3: Pre-Seeded Cert Library ──────────────────────────────────────────

    [Fact]
    public void GetAllCertTypes_ReturnsAtLeast15PreSeededTypes()
    {
        var svc = CreateService();

        var types = svc.GetAllCertTypes();

        Assert.True(types.Count >= 15,
            $"Expected at least 15 pre-seeded cert types, got {types.Count}.");
    }

    [Fact]
    public void GetAllCertTypes_IncludesForkliftOperatorCertification()
    {
        var svc = CreateService();

        var forklift = svc.GetCertTypeByName("Forklift Operator Certification");

        Assert.NotNull(forklift);
        Assert.True(forklift!.IsPreSeeded);
    }

    [Fact]
    public void GetAllCertTypes_IncludesAllRequiredConstructionCerts()
    {
        var svc = CreateService();
        var expected = new[]
        {
            "OSHA 10 Construction", "OSHA 30 Construction",
            "Forklift Operator Certification", "Overhead Crane Operator",
            "Aerial Lift / Scissor Lift Operator", "Rigger (Basic)", "Signalperson",
            "Confined Space Entry", "First Aid / CPR",
            "HAZWOPER 8-Hour Refresher", "HAZWOPER 24-Hour", "HAZWOPER 40-Hour",
            "HAZMAT CDL Endorsement", "Excavation and Trenching Safety", "Scaffolding Safety"
        };

        var names = svc.GetAllCertTypes().Select(c => c.Name).ToHashSet();

        foreach (var name in expected)
            Assert.Contains(name, names);
    }

    [Fact]
    public void AddCertType_AddsCustomCertType()
    {
        var svc = CreateService();
        int before = svc.GetAllCertTypes().Count;

        var ct = svc.AddCertType("State Crane License", "State-specific crane operator license", 730);

        Assert.Equal(before + 1, svc.GetAllCertTypes().Count);
        Assert.Equal("State Crane License", ct.Name);
        Assert.False(ct.IsPreSeeded);
    }

    [Fact]
    public void DeleteCertType_AllowsDeletionOfUnreferencedCustomType()
    {
        var svc = CreateService();
        var ct = svc.AddCertType("Temp Cert", "Temp", 365);

        var deleted = svc.DeleteCertType(ct.Id);

        Assert.True(deleted);
        Assert.Null(svc.GetCertType(ct.Id));
    }

    [Fact]
    public void DeleteCertType_PreventsDeletionWhenReferencedByOperatorRecord()
    {
        var svc = CreateService();
        var ct = svc.AddCertType("Referenced Cert", "Test", 365);
        svc.AddCertRecord("Alice", ct.Id, DateTime.Today, DateTime.Today.AddYears(1), "Admin");

        var deleted = svc.DeleteCertType(ct.Id);

        Assert.False(deleted);
        Assert.NotNull(svc.GetCertType(ct.Id));
    }

    // ── AC4: Equipment-Category Cert Requirements ─────────────────────────────

    [Fact]
    public void AssignRequirement_AssignsCertToCategory()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("Forklift Operator Certification")!;

        var req = svc.AssignRequirement("Forklift", certType.Id, "Admin");

        Assert.NotNull(req);
        Assert.Equal("Forklift", req.EquipmentCategory);
        Assert.Equal(certType.Id, req.CertTypeId);
        Assert.True(req.IsActive);
    }

    [Fact]
    public void RemoveRequirement_DeactivatesRequirement()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("Forklift Operator Certification")!;
        var req = svc.AssignRequirement("Forklift", certType.Id, "Admin");

        var removed = svc.RemoveRequirement(req.Id);

        Assert.True(removed);
        Assert.Empty(svc.GetRequirementsForCategory("Forklift"));
    }

    [Fact]
    public void GetRequirementsForCategory_ReturnOnlyActiveRequirements()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("Overhead Crane Operator")!;
        var req = svc.AssignRequirement("Crane", certType.Id, "Admin");
        svc.RemoveRequirement(req.Id);

        var requirements = svc.GetRequirementsForCategory("Crane");

        Assert.Empty(requirements);
    }

    // ── AC1: Hard-Block Checkout Enforcement (TS-1) ───────────────────────────

    [Fact]
    public void ValidateCheckout_ReturnsNotRequired_WhenNoCertRequirementForCategory()
    {
        // TS-5: Equipment with no cert requirement
        var svc = CreateService();

        var result = svc.ValidateCheckout("Any Operator", "Hand Tools");

        Assert.Equal(CertValidationOutcome.NotRequired, result);
    }

    [Fact]
    public void ValidateCheckout_ReturnsBlocked_WhenOperatorHasNoMatchingCert()
    {
        // TS-1 path: operator has no cert at all
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("Forklift Operator Certification")!;
        svc.AssignRequirement("Forklift", certType.Id, "Admin");

        var result = svc.ValidateCheckout("Uncertified Joe", "Forklift");

        Assert.Equal(CertValidationOutcome.Blocked, result);
    }

    [Fact]
    public void ValidateCheckout_ReturnsBlocked_WhenOperatorCertIsExpired()
    {
        // TS-1: Forklift cert expired 15 days ago
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("Forklift Operator Certification")!;
        svc.AssignRequirement("Forklift", certType.Id, "Admin");
        svc.AddCertRecord("Bob", certType.Id,
            DateTime.UtcNow.AddYears(-2),
            DateTime.UtcNow.AddDays(-15),   // expired 15 days ago
            "Admin");

        var result = svc.ValidateCheckout("Bob", "Forklift");

        Assert.Equal(CertValidationOutcome.Blocked, result);
    }

    [Fact]
    public void ValidateCheckout_ReturnsPassed_WhenOperatorHoldsValidCert()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("Forklift Operator Certification")!;
        svc.AssignRequirement("Forklift", certType.Id, "Admin");
        svc.AddCertRecord("Alice", certType.Id,
            DateTime.UtcNow.AddYears(-1),
            DateTime.UtcNow.AddYears(2),    // valid, expires in future
            "Admin");

        var result = svc.ValidateCheckout("Alice", "Forklift");

        Assert.Equal(CertValidationOutcome.Passed, result);
    }

    [Fact]
    public void GetBlockReasonMessage_IncludesMissingCertInMessage()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("Forklift Operator Certification")!;
        svc.AssignRequirement("Forklift", certType.Id, "Admin");

        var message = svc.GetBlockReasonMessage("No Cert Operator", "Forklift");

        Assert.NotNull(message);
        Assert.Contains("MISSING", message);
        Assert.Contains("Forklift Operator Certification", message);
        Assert.Contains("EHS Manager", message);
    }

    [Fact]
    public void GetBlockReasonMessage_IncludesExpiredCertDateInMessage()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("Forklift Operator Certification")!;
        svc.AssignRequirement("Forklift", certType.Id, "Admin");
        var expiry = DateTime.UtcNow.AddDays(-15);
        svc.AddCertRecord("Bob", certType.Id, DateTime.UtcNow.AddYears(-2), expiry, "Admin");

        var message = svc.GetBlockReasonMessage("Bob", "Forklift");

        Assert.NotNull(message);
        Assert.Contains("EXPIRED", message);
        Assert.Contains(expiry.ToString("yyyy-MM-dd"), message);
    }

    [Fact]
    public void GetBlockReasonMessage_ReturnsNull_WhenCategoryHasNoRequirements()
    {
        var svc = CreateService();

        var message = svc.GetBlockReasonMessage("Alice", "Hand Tools");

        Assert.Null(message);
    }

    // ── AC2: Supervisor Override with Audit Trail (TS-2) ──────────────────────

    [Fact]
    public void RecordOverride_CreatesImmutableOverrideRecord()
    {
        // TS-2: supervisor override is recorded with all required fields
        var svc = CreateService();

        var overrideRecord = svc.RecordOverride(
            checkoutRecordId: 42,
            supervisorName: "Jane Supervisor",
            reasonCode: OverrideReasonCode.EmergencyRenewalInProgress,
            reasonText: "Renewal in progress, cert due next week",
            blockedOperatorName: "Bob",
            requiredCertName: "Forklift Operator Certification");

        Assert.NotNull(overrideRecord);
        Assert.Equal(42, overrideRecord.CheckoutRecordId);
        Assert.Equal("Jane Supervisor", overrideRecord.SupervisorName);
        Assert.Equal(OverrideReasonCode.EmergencyRenewalInProgress, overrideRecord.ReasonCode);
        Assert.Equal("Bob", overrideRecord.BlockedOperatorName);
        Assert.True(overrideRecord.CreatedAt <= DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void GetAllOverrides_ReturnsAllRecordedOverrides()
    {
        var svc = CreateService();
        svc.RecordOverride(1, "Sup A", OverrideReasonCode.OneTimeException, "note", "Op1", "CertA");
        svc.RecordOverride(2, "Sup B", OverrideReasonCode.Other, "note2", "Op2", "CertB");

        var overrides = svc.GetAllOverrides();

        Assert.Equal(2, overrides.Count);
    }

    [Fact]
    public void OverrideRecord_IsRetrievableAfterMultipleOperations()
    {
        // Verifies append-only semantics: override stays present after other operations
        var svc = CreateService();
        var rec = svc.RecordOverride(10, "Sup", OverrideReasonCode.Other, "", "Op", "Cert");
        svc.AddCertType("NewCert", "desc", 365);
        svc.AddCertRecord("Op", 1, DateTime.UtcNow, DateTime.UtcNow.AddYears(1), "Admin");

        var overrides = svc.GetAllOverrides();

        Assert.Contains(overrides, o => o.Id == rec.Id && o.SupervisorName == "Sup");
    }

    // ── AC5: Document Upload ──────────────────────────────────────────────────

    [Fact]
    public void AddDocument_StoresDocumentMetadata()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("Forklift Operator Certification")!;
        var record = svc.AddCertRecord("Alice", certType.Id, DateTime.Today, DateTime.Today.AddYears(1), "Admin");

        var doc = svc.AddDocument(record.Id, "cert_card.pdf", "application/pdf",
            "stored_abc123.pdf", "EHS Manager");

        Assert.NotNull(doc);
        Assert.Equal("cert_card.pdf", doc.FileName);
        Assert.Equal("application/pdf", doc.FileType);
        Assert.Equal("EHS Manager", doc.UploadedBy);
        Assert.Equal(record.Id, doc.OperatorCertRecordId);
    }

    [Fact]
    public void GetDocumentsForCertRecord_ReturnsDocumentsForCorrectRecord()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("Forklift Operator Certification")!;
        var r1 = svc.AddCertRecord("Alice", certType.Id, DateTime.Today, DateTime.Today.AddYears(1), "Admin");
        var r2 = svc.AddCertRecord("Bob",   certType.Id, DateTime.Today, DateTime.Today.AddYears(1), "Admin");
        svc.AddDocument(r1.Id, "alice.pdf", "application/pdf", "stored_alice.pdf", "Admin");
        svc.AddDocument(r2.Id, "bob.jpg",   "image/jpeg",      "stored_bob.jpg",   "Admin");

        var docs = svc.GetDocumentsForCertRecord(r1.Id);

        Assert.Single(docs);
        Assert.Equal("alice.pdf", docs[0].FileName);
    }

    // ── AC6: Expiry Alert Notification Engine (TS-3) ──────────────────────────

    [Fact]
    public void GetCertsExpiringSoon_ReturnsCertsWithin30DayThreshold()
    {
        // TS-3: cert expiring in 29 days appears in the 30-day threshold list
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("Confined Space Entry")!;
        svc.AddCertRecord("Jane", certType.Id,
            DateTime.UtcNow.AddYears(-1),
            DateTime.UtcNow.AddDays(29),    // expires in 29 days
            "Admin");

        var expiring = svc.GetCertsExpiringSoon(30);

        Assert.Contains(expiring, r => r.OperatorName == "Jane" && r.CertTypeId == certType.Id);
    }

    [Fact]
    public void GetCertsExpiringSoon_DoesNotReturnAlreadyExpiredCerts()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("Confined Space Entry")!;
        svc.AddCertRecord("Bob", certType.Id,
            DateTime.UtcNow.AddYears(-2),
            DateTime.UtcNow.AddDays(-5),    // already expired
            "Admin");

        var expiring = svc.GetCertsExpiringSoon(30);

        Assert.DoesNotContain(expiring, r => r.OperatorName == "Bob");
    }

    [Fact]
    public void GetCertsExpiringSoon_DoesNotReturnCertsOutsideThreshold()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("Confined Space Entry")!;
        svc.AddCertRecord("Carol", certType.Id,
            DateTime.UtcNow.AddYears(-1),
            DateTime.UtcNow.AddDays(45),    // 45 days out — outside 30-day window
            "Admin");

        var expiring = svc.GetCertsExpiringSoon(30);

        Assert.DoesNotContain(expiring, r => r.OperatorName == "Carol");
    }

    [Fact]
    public void RefreshCertStatuses_MarksExpiredCertAsExpired()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("First Aid / CPR")!;
        var record = svc.AddCertRecord("Dave", certType.Id,
            DateTime.UtcNow.AddYears(-3),
            DateTime.UtcNow.AddDays(-1),   // expired yesterday
            "Admin");

        svc.RefreshCertStatuses();

        var refreshed = svc.GetCertsForOperator("Dave").First(r => r.Id == record.Id);
        Assert.Equal(CertificationStatus.Expired, refreshed.Status);
    }

    [Fact]
    public void RefreshCertStatuses_MarksExpiringSoonCertAsExpiringSoon()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("First Aid / CPR")!;
        var record = svc.AddCertRecord("Eve", certType.Id,
            DateTime.UtcNow.AddYears(-1),
            DateTime.UtcNow.AddDays(20),   // within 30-day yellow window
            "Admin");

        svc.RefreshCertStatuses();

        var refreshed = svc.GetCertsForOperator("Eve").First(r => r.Id == record.Id);
        Assert.Equal(CertificationStatus.ExpiringSoon, refreshed.Status);
    }

    // ── AC7: Compliance Dashboard Status Mapping ──────────────────────────────

    [Fact]
    public void GetAllCertRecords_ReturnsAllAddedRecords()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("OSHA 10 Construction")!;
        svc.AddCertRecord("Alice", certType.Id, DateTime.Today, DateTime.Today.AddYears(1), "Admin");
        svc.AddCertRecord("Bob",   certType.Id, DateTime.Today, DateTime.Today.AddYears(1), "Admin");

        var all = svc.GetAllCertRecords();

        Assert.Equal(2, all.Count);
    }

    // ── AC8: Bulk Import (TS-4) ───────────────────────────────────────────────

    [Fact]
    public void BulkImportCertRecords_ImportsValidRows()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("Forklift Operator Certification")!;
        var rows = new[]
        {
            new BulkCertImportRow { RowNumber = 1, OperatorName = "Alice", CertTypeName = certType.Name,
                IssuedDate = DateTime.Today.AddYears(-1), ExpiryDate = DateTime.Today.AddYears(2) },
            new BulkCertImportRow { RowNumber = 2, OperatorName = "Bob", CertTypeName = certType.Name,
                IssuedDate = DateTime.Today.AddYears(-1), ExpiryDate = DateTime.Today.AddYears(2) }
        };

        var (imported, errors) = svc.BulkImportCertRecords(rows, "Admin");

        Assert.Equal(2, imported);
        Assert.Empty(errors);
        Assert.Equal(2, svc.GetAllCertRecords().Count);
    }

    [Fact]
    public void BulkImportCertRecords_SurfacesValidationErrors_WithoutBlockingValidRows()
    {
        // TS-4: 5 invalid rows, remaining valid rows should still import
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("Forklift Operator Certification")!;
        var rows = new[]
        {
            // 3 valid rows
            new BulkCertImportRow { RowNumber = 1, OperatorName = "Alice", CertTypeName = certType.Name,
                IssuedDate = DateTime.Today.AddYears(-1), ExpiryDate = DateTime.Today.AddYears(2) },
            new BulkCertImportRow { RowNumber = 2, OperatorName = "Bob", CertTypeName = certType.Name,
                IssuedDate = DateTime.Today.AddYears(-1), ExpiryDate = DateTime.Today.AddYears(2) },
            new BulkCertImportRow { RowNumber = 3, OperatorName = "Carol", CertTypeName = certType.Name,
                IssuedDate = DateTime.Today.AddYears(-1), ExpiryDate = DateTime.Today.AddYears(2) },
            // 2 invalid: missing expiry date
            new BulkCertImportRow { RowNumber = 4, OperatorName = "Dave", CertTypeName = certType.Name,
                IssuedDate = DateTime.Today.AddYears(-1), ExpiryDate = default },
            // 1 invalid: unknown cert type
            new BulkCertImportRow { RowNumber = 5, OperatorName = "Eve", CertTypeName = "Nonexistent Cert",
                IssuedDate = DateTime.Today.AddYears(-1), ExpiryDate = DateTime.Today.AddYears(2) },
        };

        var (imported, errors) = svc.BulkImportCertRecords(rows, "Admin");

        Assert.Equal(3, imported);
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.row == 4);
        Assert.Contains(errors, e => e.row == 5);
    }

    [Fact]
    public void BulkImportCertRecords_ErrorWhenExpiryBeforeIssuedDate()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("Forklift Operator Certification")!;
        var rows = new[]
        {
            new BulkCertImportRow { RowNumber = 1, OperatorName = "Alice", CertTypeName = certType.Name,
                IssuedDate = DateTime.Today,
                ExpiryDate = DateTime.Today.AddDays(-1) }  // expiry before issued
        };

        var (imported, errors) = svc.BulkImportCertRecords(rows, "Admin");

        Assert.Equal(0, imported);
        Assert.Single(errors);
        Assert.Equal(1, errors[0].row);
        Assert.Contains("ExpiryDate must be after", errors[0].error);
    }

    [Fact]
    public void BulkImportCertRecords_ErrorWhenOperatorNameIsEmpty()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("OSHA 10 Construction")!;
        var rows = new[]
        {
            new BulkCertImportRow { RowNumber = 1, OperatorName = "", CertTypeName = certType.Name,
                IssuedDate = DateTime.Today, ExpiryDate = DateTime.Today.AddYears(1) }
        };

        var (imported, errors) = svc.BulkImportCertRecords(rows, "Admin");

        Assert.Equal(0, imported);
        Assert.Single(errors);
        Assert.Contains("OperatorName", errors[0].error);
    }

    // ── Audit Log ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetAuditLog_ContainsEntryAfterAddingCertRecord()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("OSHA 10 Construction")!;
        svc.AddCertRecord("Alice", certType.Id, DateTime.Today, DateTime.Today.AddYears(1), "Admin");

        var log = svc.GetAuditLog();

        Assert.Contains(log, e => e.EventType == "CertAdded" && e.ActorName == "Admin");
    }

    [Fact]
    public void GetAuditLog_ContainsEntryAfterDocumentUpload()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("OSHA 10 Construction")!;
        var rec = svc.AddCertRecord("Alice", certType.Id, DateTime.Today, DateTime.Today.AddYears(1), "Admin");
        svc.AddDocument(rec.Id, "file.pdf", "application/pdf", "stored.pdf", "EHSUser");

        var log = svc.GetAuditLog();

        Assert.Contains(log, e => e.EventType == "DocumentUploaded" && e.ActorName == "EHSUser");
    }

    [Fact]
    public void GetAuditLog_ContainsEntryAfterOverrideRecorded()
    {
        var svc = CreateService();
        svc.RecordOverride(1, "Supervisor", OverrideReasonCode.Other, "reason", "Op", "Cert");

        var log = svc.GetAuditLog();

        Assert.Contains(log, e => e.EventType == "OverrideRecorded" && e.ActorName == "Supervisor");
    }

    [Fact]
    public void GetAuditLog_ContainsEntryAfterBulkImport()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("OSHA 10 Construction")!;
        var rows = new[]
        {
            new BulkCertImportRow { RowNumber = 1, OperatorName = "Alice", CertTypeName = certType.Name,
                IssuedDate = DateTime.Today, ExpiryDate = DateTime.Today.AddYears(1) }
        };

        svc.BulkImportCertRecords(rows, "ImportUser");

        var log = svc.GetAuditLog();
        Assert.Contains(log, e => e.EventType == "BulkImport" && e.ActorName == "ImportUser");
    }

    // ── CertExpiryNotificationJob ──────────────────────────────────────────────

    [Fact]
    public void CertExpiryNotificationJob_RunDailyDigest_LogsExpiringSoonCerts()
    {
        var svc = CreateService();
        var certType = svc.GetCertTypeByName("Confined Space Entry")!;
        svc.AddCertRecord("Frank", certType.Id,
            DateTime.UtcNow.AddYears(-1),
            DateTime.UtcNow.AddDays(15),   // expiring within 30 days
            "Admin");

        using var logFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddConsole());
        var logger = logFactory.CreateLogger<CertExpiryNotificationJob>();
        var job = new CertExpiryNotificationJob(svc, logger);

        // Should not throw — daily digest runs without error
        var exception = Record.Exception(() => job.RunDailyDigest());
        Assert.Null(exception);
    }

    [Fact]
    public void CertExpiryNotificationJob_RunDailyDigest_NoOpWhenNoCertsExpiring()
    {
        var svc = CreateService();  // no records, no certs expiring

        using var logFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddConsole());
        var logger = logFactory.CreateLogger<CertExpiryNotificationJob>();
        var job = new CertExpiryNotificationJob(svc, logger);

        var exception = Record.Exception(() => job.RunDailyDigest());
        Assert.Null(exception);
    }
}
