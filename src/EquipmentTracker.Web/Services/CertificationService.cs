using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-memory implementation of <see cref="ICertificationService"/>.
/// Pre-seeds 15 construction/industrial certification types on first use.
/// All audit log and override records are append-only (never modified or deleted).
/// Registered as a singleton in DI so all lists survive the application lifetime.
/// </summary>
public class CertificationService : ICertificationService
{
    // ── Storage ───────────────────────────────────────────────────────────────

    private readonly List<CertificationType> _certTypes = new();
    private readonly List<OperatorCertRecord> _certRecords = new();
    private readonly List<CertDocument> _documents = new();
    private readonly List<EquipmentCategoryCertRequirement> _requirements = new();
    private readonly List<CheckoutOverrideRecord> _overrides = new();
    private readonly List<CertAuditLogEntry> _auditLog = new();

    private int _nextCertTypeId = 1;
    private int _nextCertRecordId = 1;
    private int _nextDocId = 1;
    private int _nextRequirementId = 1;
    private int _nextOverrideId = 1;
    private int _nextAuditId = 1;

    private const int MaxCustomCertTypes = 50;
    private const int ExpiryYellowDays = 30;

    // ── Constructor — pre-seed 15 cert types ─────────────────────────────────

    public CertificationService()
    {
        SeedCertTypes();
    }

    private void SeedCertTypes()
    {
        var preSeeded = new[]
        {
            ("OSHA 10 Construction",              "OSHA 10-hour Construction Safety course",                         730),
            ("OSHA 30 Construction",              "OSHA 30-hour Construction Safety course",                         1825),
            ("Forklift Operator Certification",   "Powered industrial truck (forklift) operator certification",       1095),
            ("Overhead Crane Operator",           "Overhead / bridge crane operator qualification",                   1095),
            ("Aerial Lift / Scissor Lift Operator","Aerial work platform (AWP) and scissor lift operator cert",       1095),
            ("Rigger (Basic)",                    "Basic rigging qualification for attaching loads to a crane",       1825),
            ("Signalperson",                      "Qualified signalperson for crane and rigging operations",          1825),
            ("Confined Space Entry",              "Permit-required confined space entry authorisation",               365),
            ("First Aid / CPR",                   "Basic first aid and CPR/AED certification",                        730),
            ("HAZWOPER 8-Hour Refresher",         "HAZWOPER annual 8-hour refresher (29 CFR 1910.120)",               365),
            ("HAZWOPER 24-Hour",                  "HAZWOPER 24-hour Operations Level training",                       1095),
            ("HAZWOPER 40-Hour",                  "HAZWOPER 40-hour General Site Worker training",                    1095),
            ("HAZMAT CDL Endorsement",            "Commercial driver's licence HAZMAT endorsement",                   1825),
            ("Excavation and Trenching Safety",   "Competent person for excavation and trenching safety",             1095),
            ("Scaffolding Safety",                "Competent person for scaffold erection and inspection",            1095),
        };

        foreach (var (name, desc, days) in preSeeded)
        {
            _certTypes.Add(new CertificationType
            {
                Id = _nextCertTypeId++,
                Name = name,
                Description = desc,
                RenewalPeriodDays = days,
                IsPreSeeded = true,
                IsDeletable = true,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    // ── Certification Library ─────────────────────────────────────────────────

    public IReadOnlyList<CertificationType> GetAllCertTypes() =>
        _certTypes.AsReadOnly();

    public CertificationType? GetCertType(int id) =>
        _certTypes.FirstOrDefault(c => c.Id == id);

    public CertificationType? GetCertTypeByName(string name) =>
        _certTypes.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public CertificationType AddCertType(string name, string description, int renewalPeriodDays)
    {
        int customCount = _certTypes.Count(c => !c.IsPreSeeded);
        if (customCount >= MaxCustomCertTypes)
            throw new InvalidOperationException(
                $"Custom cert type limit of {MaxCustomCertTypes} per account has been reached.");

        var ct = new CertificationType
        {
            Id = _nextCertTypeId++,
            Name = name,
            Description = description,
            RenewalPeriodDays = renewalPeriodDays,
            IsPreSeeded = false,
            IsDeletable = true,
            CreatedAt = DateTime.UtcNow
        };
        _certTypes.Add(ct);
        return ct;
    }

    public bool DeleteCertType(int id)
    {
        var ct = _certTypes.FirstOrDefault(c => c.Id == id);
        if (ct is null) return false;

        bool isReferenced = _certRecords.Any(r => r.CertTypeId == id);
        if (isReferenced)
        {
            ct.IsDeletable = false;
            return false;
        }

        _certTypes.Remove(ct);
        return true;
    }

    // ── Operator Certification Records ────────────────────────────────────────

    public IReadOnlyList<OperatorCertRecord> GetCertsForOperator(string operatorName) =>
        _certRecords
            .Where(r => r.OperatorName.Equals(operatorName, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();

    public IReadOnlyList<OperatorCertRecord> GetAllCertRecords() =>
        _certRecords.AsReadOnly();

    public OperatorCertRecord AddCertRecord(
        string operatorName,
        int certTypeId,
        DateTime issuedDate,
        DateTime expiryDate,
        string addedBy,
        int? operatorUserId = null,
        string? notes = null)
    {
        var status = ComputeStatus(expiryDate);

        // Mark the cert type non-deletable since it's now referenced
        var certType = _certTypes.FirstOrDefault(c => c.Id == certTypeId);
        if (certType is not null)
            certType.IsDeletable = false;

        var record = new OperatorCertRecord
        {
            Id = _nextCertRecordId++,
            OperatorUserId = operatorUserId,
            OperatorName = operatorName,
            CertTypeId = certTypeId,
            IssuedDate = issuedDate,
            ExpiryDate = expiryDate,
            Status = status,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _certRecords.Add(record);

        AppendAudit("OperatorCertRecord", record.Id, addedBy, "CertAdded", null,
            $"Operator={operatorName}, CertTypeId={certTypeId}, Expiry={expiryDate:yyyy-MM-dd}");

        return record;
    }

    // ── Document Upload ───────────────────────────────────────────────────────

    public CertDocument AddDocument(
        int certRecordId,
        string fileName,
        string fileType,
        string storedFileName,
        string uploadedBy)
    {
        var doc = new CertDocument
        {
            Id = _nextDocId++,
            OperatorCertRecordId = certRecordId,
            FileName = fileName,
            FileType = fileType,
            StoredFileName = storedFileName,
            UploadedBy = uploadedBy,
            CreatedAt = DateTime.UtcNow
        };
        _documents.Add(doc);

        AppendAudit("CertDocument", doc.Id, uploadedBy, "DocumentUploaded", null,
            $"FileName={fileName}, CertRecordId={certRecordId}");

        return doc;
    }

    public IReadOnlyList<CertDocument> GetDocumentsForCertRecord(int certRecordId) =>
        _documents.Where(d => d.OperatorCertRecordId == certRecordId).ToList().AsReadOnly();

    // ── Equipment-Category Cert Requirements ──────────────────────────────────

    public IReadOnlyList<EquipmentCategoryCertRequirement> GetRequirementsForCategory(string category) =>
        _requirements
            .Where(r => r.IsActive &&
                        r.EquipmentCategory.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();

    public IReadOnlyList<EquipmentCategoryCertRequirement> GetAllRequirements() =>
        _requirements.AsReadOnly();

    public EquipmentCategoryCertRequirement AssignRequirement(
        string category, int certTypeId, string createdBy)
    {
        var req = new EquipmentCategoryCertRequirement
        {
            Id = _nextRequirementId++,
            EquipmentCategory = category,
            CertTypeId = certTypeId,
            IsActive = true,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };
        _requirements.Add(req);
        return req;
    }

    public bool RemoveRequirement(int id)
    {
        var req = _requirements.FirstOrDefault(r => r.Id == id);
        if (req is null) return false;
        req.IsActive = false;
        return true;
    }

    // ── Checkout Cert Validation ──────────────────────────────────────────────

    public CertValidationOutcome ValidateCheckout(string operatorName, string equipmentCategory)
    {
        var requirements = GetRequirementsForCategory(equipmentCategory);
        if (!requirements.Any())
            return CertValidationOutcome.NotRequired;

        RefreshCertStatuses();
        var operatorCerts = GetCertsForOperator(operatorName);

        foreach (var req in requirements)
        {
            var matching = operatorCerts.FirstOrDefault(c =>
                c.CertTypeId == req.CertTypeId &&
                c.Status != CertificationStatus.Expired);

            if (matching is null)
                return CertValidationOutcome.Blocked;
        }

        return CertValidationOutcome.Passed;
    }

    public string? GetBlockReasonMessage(string operatorName, string equipmentCategory)
    {
        var requirements = GetRequirementsForCategory(equipmentCategory);
        if (!requirements.Any())
            return null;

        RefreshCertStatuses();
        var operatorCerts = GetCertsForOperator(operatorName);

        var blockedParts = new List<string>();
        foreach (var req in requirements)
        {
            var certType = GetCertType(req.CertTypeId);
            var certTypeName = certType?.Name ?? $"cert type #{req.CertTypeId}";

            var matching = operatorCerts.FirstOrDefault(c => c.CertTypeId == req.CertTypeId);
            if (matching is null)
            {
                blockedParts.Add(
                    $"MISSING: {certTypeName} — no certification record on file for {operatorName}.");
            }
            else if (matching.Status == CertificationStatus.Expired)
            {
                blockedParts.Add(
                    $"EXPIRED: {certTypeName} — expired on {matching.ExpiryDate:yyyy-MM-dd}.");
            }
        }

        if (!blockedParts.Any())
            return null;

        return "Checkout blocked. " +
               string.Join(" ", blockedParts) +
               " Contact your EHS Manager to resolve certification issues.";
    }

    // ── Supervisor Override ───────────────────────────────────────────────────

    public CheckoutOverrideRecord RecordOverride(
        int checkoutRecordId,
        string supervisorName,
        OverrideReasonCode reasonCode,
        string reasonText,
        string blockedOperatorName,
        string requiredCertName,
        int? supervisorUserId = null)
    {
        // Append-only — no update/delete permitted after creation
        var record = new CheckoutOverrideRecord
        {
            Id = _nextOverrideId++,
            CheckoutRecordId = checkoutRecordId,
            SupervisorName = supervisorName,
            SupervisorUserId = supervisorUserId,
            ReasonCode = reasonCode,
            ReasonText = reasonText,
            BlockedOperatorName = blockedOperatorName,
            RequiredCertName = requiredCertName,
            CreatedAt = DateTime.UtcNow
        };
        _overrides.Add(record);

        AppendAudit("CheckoutOverrideRecord", record.Id, supervisorName, "OverrideRecorded", null,
            $"Checkout={checkoutRecordId}, Operator={blockedOperatorName}, ReasonCode={reasonCode}");

        return record;
    }

    public IReadOnlyList<CheckoutOverrideRecord> GetAllOverrides() =>
        _overrides.AsReadOnly();

    // ── Compliance Dashboard ──────────────────────────────────────────────────

    public void RefreshCertStatuses()
    {
        foreach (var record in _certRecords)
        {
            var newStatus = ComputeStatus(record.ExpiryDate);
            if (newStatus != record.Status)
            {
                var before = record.Status.ToString();
                record.Status = newStatus;
                record.UpdatedAt = DateTime.UtcNow;
                AppendAudit("OperatorCertRecord", record.Id, "System", "CertStatusChanged",
                    before, newStatus.ToString());
            }
        }
    }

    public IReadOnlyList<OperatorCertRecord> GetCertsExpiringSoon(int daysThreshold)
    {
        RefreshCertStatuses();
        var threshold = DateTime.UtcNow.AddDays(daysThreshold);
        return _certRecords
            .Where(r => r.Status != CertificationStatus.Expired &&
                        r.ExpiryDate <= threshold &&
                        r.ExpiryDate >= DateTime.UtcNow)
            .ToList()
            .AsReadOnly();
    }

    // ── Bulk Import ───────────────────────────────────────────────────────────

    public (int imported, List<(int row, string error)> errors) BulkImportCertRecords(
        IEnumerable<BulkCertImportRow> rows, string importedBy)
    {
        int imported = 0;
        var errors = new List<(int, string)>();

        foreach (var row in rows)
        {
            var rowErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(row.OperatorName))
                rowErrors.Add("OperatorName is required.");

            if (string.IsNullOrWhiteSpace(row.CertTypeName))
            {
                rowErrors.Add("CertTypeName is required.");
            }
            else
            {
                var certType = GetCertTypeByName(row.CertTypeName);
                if (certType is null)
                    rowErrors.Add($"CertTypeName '{row.CertTypeName}' not found in the certification library.");
            }

            if (row.ExpiryDate == default)
                rowErrors.Add("ExpiryDate is required and must be a valid date.");

            if (row.IssuedDate == default)
                rowErrors.Add("IssuedDate is required and must be a valid date.");

            if (row.IssuedDate != default && row.ExpiryDate != default && row.ExpiryDate <= row.IssuedDate)
                rowErrors.Add("ExpiryDate must be after IssuedDate.");

            if (rowErrors.Any())
            {
                errors.Add((row.RowNumber, string.Join(" ", rowErrors)));
                continue;
            }

            var certType2 = GetCertTypeByName(row.CertTypeName)!;
            AddCertRecord(row.OperatorName, certType2.Id, row.IssuedDate, row.ExpiryDate, importedBy,
                notes: row.Notes);
            imported++;
        }

        AppendAudit("BulkImport", 0, importedBy, "BulkImport", null,
            $"Imported={imported}, Errors={errors.Count}");

        return (imported, errors);
    }

    // ── Audit Log ─────────────────────────────────────────────────────────────

    public IReadOnlyList<CertAuditLogEntry> GetAuditLog() =>
        _auditLog.OrderByDescending(e => e.CreatedAt).ToList().AsReadOnly();

    // ── Private Helpers ───────────────────────────────────────────────────────

    private static CertificationStatus ComputeStatus(DateTime expiryDate)
    {
        var now = DateTime.UtcNow;
        if (expiryDate < now)
            return CertificationStatus.Expired;
        if (expiryDate < now.AddDays(ExpiryYellowDays))
            return CertificationStatus.ExpiringSoon;
        return CertificationStatus.Active;
    }

    private void AppendAudit(
        string entityType,
        int entityId,
        string actorName,
        string eventType,
        string? before,
        string? after)
    {
        _auditLog.Add(new CertAuditLogEntry
        {
            Id = _nextAuditId++,
            EntityType = entityType,
            EntityId = entityId,
            ActorName = actorName,
            EventType = eventType,
            BeforeState = before,
            AfterState = after,
            CreatedAt = DateTime.UtcNow
        });
    }
}
