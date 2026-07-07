using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Service contract for the Operator Certification &amp; Compliance Enforcement feature (Issue #120).
/// Manages the certification library, operator cert records, document uploads, equipment-category
/// requirements, checkout validation, supervisor overrides, compliance dashboard data,
/// bulk import, expiry alerting, and the append-only audit log.
/// </summary>
public interface ICertificationService
{
    // ── Certification Library ─────────────────────────────────────────────────

    /// <summary>Returns all cert types (pre-seeded + custom).</summary>
    IReadOnlyList<CertificationType> GetAllCertTypes();

    /// <summary>Returns a single cert type by ID, or null if not found.</summary>
    CertificationType? GetCertType(int id);

    /// <summary>Returns a single cert type by exact name (case-insensitive), or null.</summary>
    CertificationType? GetCertTypeByName(string name);

    /// <summary>
    /// Adds a new custom cert type.
    /// Throws <see cref="InvalidOperationException"/> if the account's custom cert type limit (50) is reached.
    /// </summary>
    CertificationType AddCertType(string name, string description, int renewalPeriodDays);

    /// <summary>
    /// Deletes a cert type by ID.
    /// Returns false if the type does not exist or is currently referenced by an operator cert record.
    /// </summary>
    bool DeleteCertType(int id);

    // ── Operator Certification Records ────────────────────────────────────────

    /// <summary>Returns all cert records for a given operator (by name, case-insensitive).</summary>
    IReadOnlyList<OperatorCertRecord> GetCertsForOperator(string operatorName);

    /// <summary>Returns all operator cert records across all operators.</summary>
    IReadOnlyList<OperatorCertRecord> GetAllCertRecords();

    /// <summary>
    /// Adds a new certification record for an operator.
    /// Logs a CertAdded audit entry.
    /// </summary>
    OperatorCertRecord AddCertRecord(
        string operatorName,
        int certTypeId,
        DateTime issuedDate,
        DateTime expiryDate,
        string addedBy,
        int? operatorUserId = null,
        string? notes = null);

    // ── Document Upload ───────────────────────────────────────────────────────

    /// <summary>
    /// Adds a cert document metadata record (append-only).
    /// Logs a DocumentUploaded audit entry.
    /// Supported fileType values: "application/pdf", "image/jpeg", "image/png".
    /// </summary>
    CertDocument AddDocument(int certRecordId, string fileName, string fileType, string storedFileName, string uploadedBy);

    /// <summary>Returns all documents attached to the specified cert record.</summary>
    IReadOnlyList<CertDocument> GetDocumentsForCertRecord(int certRecordId);

    // ── Equipment-Category Cert Requirements ──────────────────────────────────

    /// <summary>Returns active cert requirements for the given equipment category.</summary>
    IReadOnlyList<EquipmentCategoryCertRequirement> GetRequirementsForCategory(string category);

    /// <summary>Returns all cert requirements across all categories.</summary>
    IReadOnlyList<EquipmentCategoryCertRequirement> GetAllRequirements();

    /// <summary>Assigns a cert type as a requirement for an equipment category.</summary>
    EquipmentCategoryCertRequirement AssignRequirement(string category, int certTypeId, string createdBy);

    /// <summary>
    /// Removes (deactivates) a cert requirement by ID.
    /// Returns false if the ID is not found.
    /// </summary>
    bool RemoveRequirement(int id);

    // ── Checkout Cert Validation ──────────────────────────────────────────────

    /// <summary>
    /// Validates whether <paramref name="operatorName"/> can check out an item
    /// in <paramref name="equipmentCategory"/>.
    /// Returns NotRequired, Passed, or Blocked.
    /// Adds no more than 200 ms of latency (fast in-memory lookup).
    /// </summary>
    CertValidationOutcome ValidateCheckout(string operatorName, string equipmentCategory);

    /// <summary>
    /// Returns a human-readable block message describing which cert is required and its status.
    /// Returns null if there is no block.
    /// </summary>
    string? GetBlockReasonMessage(string operatorName, string equipmentCategory);

    // ── Supervisor Override ───────────────────────────────────────────────────

    /// <summary>
    /// Records an immutable supervisor override for a blocked checkout.
    /// Logs an OverrideRecorded audit entry.
    /// The returned record must never be modified or deleted.
    /// </summary>
    CheckoutOverrideRecord RecordOverride(
        int checkoutRecordId,
        string supervisorName,
        OverrideReasonCode reasonCode,
        string reasonText,
        string blockedOperatorName,
        string requiredCertName,
        int? supervisorUserId = null);

    /// <summary>Returns all supervisor override records (append-only, audit-ready).</summary>
    IReadOnlyList<CheckoutOverrideRecord> GetAllOverrides();

    // ── Compliance Dashboard ──────────────────────────────────────────────────

    /// <summary>
    /// Refreshes the computed <see cref="CertificationStatus"/> on every <see cref="OperatorCertRecord"/>
    /// based on the current UTC date:
    /// <list type="bullet">
    ///   <item>Expired — expiry date is in the past.</item>
    ///   <item>ExpiringSoon — expiry date is within 30 days.</item>
    ///   <item>Active — everything else.</item>
    /// </list>
    /// Logs a CertStatusChanged audit entry for each record whose status changes.
    /// </summary>
    void RefreshCertStatuses();

    /// <summary>
    /// Returns operator cert records whose expiry date falls within
    /// <paramref name="daysThreshold"/> calendar days from now.
    /// Used by the expiry alert scheduled job.
    /// </summary>
    IReadOnlyList<OperatorCertRecord> GetCertsExpiringSoon(int daysThreshold);

    // ── Bulk Import ───────────────────────────────────────────────────────────

    /// <summary>
    /// Imports up to 1,000 cert records from the pre-parsed rows.
    /// Valid rows are imported; invalid rows are returned as (rowNumber, errorMessage) tuples.
    /// Valid and invalid rows are independent — valid rows always import even if others fail.
    /// Logs a BulkImport audit entry for the batch.
    /// </summary>
    (int imported, List<(int row, string error)> errors) BulkImportCertRecords(
        IEnumerable<BulkCertImportRow> rows,
        string importedBy);

    // ── Audit Log ─────────────────────────────────────────────────────────────

    /// <summary>Returns all cert audit log entries, newest first.</summary>
    IReadOnlyList<CertAuditLogEntry> GetAuditLog();
}
