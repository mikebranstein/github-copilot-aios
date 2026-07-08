using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Controller for the Operator Certification &amp; Compliance Enforcement feature (Issue #120).
/// Covers:
///  - Compliance dashboard (AC7)
///  - Certification library management (AC3, AC4)
///  - Operator certification profile and document upload (AC5)
///  - Bulk CSV import (AC8)
///  - Override audit log (AC2)
/// </summary>
public class CertificationController : Controller
{
    private readonly ICertificationService _certService;

    public CertificationController(ICertificationService certService)
    {
        _certService = certService;
    }

    // ── Compliance Dashboard (AC7) ────────────────────────────────────────────

    /// <summary>GET /Certification — Color-coded compliance dashboard.</summary>
    public IActionResult Index()
    {
        _certService.RefreshCertStatuses();

        var allRecords = _certService.GetAllCertRecords();
        var certTypeById = _certService.GetAllCertTypes().ToDictionary(c => c.Id, c => c);

        var operatorRows = allRecords
            .GroupBy(r => r.OperatorName, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var entries = g.Select(r => new CertComplianceEntry
                {
                    RecordId = r.Id,
                    CertTypeName = certTypeById.TryGetValue(r.CertTypeId, out var ct) ? ct.Name : $"#{r.CertTypeId}",
                    ExpiryDate = r.ExpiryDate,
                    Status = r.Status
                }).ToList();

                var overallColor = entries.Any(e => e.Color == ComplianceColor.Red) ? ComplianceColor.Red
                                 : entries.Any(e => e.Color == ComplianceColor.Yellow) ? ComplianceColor.Yellow
                                 : ComplianceColor.Green;

                return new OperatorComplianceRow
                {
                    OperatorName = g.Key,
                    Certs = entries,
                    OverallColor = overallColor
                };
            })
            .OrderBy(r => r.OverallColor)   // RED first
            .ThenBy(r => r.OperatorName)
            .ToList();

        var model = new ComplianceDashboardViewModel
        {
            Operators = operatorRows,
            TotalOperators = operatorRows.Count,
            RedCount = operatorRows.Count(r => r.OverallColor == ComplianceColor.Red),
            YellowCount = operatorRows.Count(r => r.OverallColor == ComplianceColor.Yellow),
            GreenCount = operatorRows.Count(r => r.OverallColor == ComplianceColor.Green)
        };

        return View(model);
    }

    // ── Certification Library (AC3, AC4) ──────────────────────────────────────

    /// <summary>GET /Certification/CertLibrary</summary>
    public IActionResult CertLibrary()
    {
        var model = BuildCertLibraryViewModel();
        return View(model);
    }

    /// <summary>POST /Certification/AddCertType — Adds a custom cert type.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddCertType(AddCertTypeForm form)
    {
        if (!ModelState.IsValid)
        {
            var vm = BuildCertLibraryViewModel();
            vm.NewCertType = form;
            return View("CertLibrary", vm);
        }

        try
        {
            _certService.AddCertType(form.Name, form.Description, form.RenewalPeriodDays);
            TempData["SuccessMessage"] = $"Certification type '{form.Name}' added.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(CertLibrary));
    }

    /// <summary>POST /Certification/DeleteCertType/5</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteCertType(int id)
    {
        var ct = _certService.GetCertType(id);
        if (ct is null) return NotFound();

        var deleted = _certService.DeleteCertType(id);
        TempData[deleted ? "SuccessMessage" : "ErrorMessage"] = deleted
            ? $"Certification type '{ct.Name}' deleted."
            : $"'{ct.Name}' cannot be deleted because it is referenced by existing operator records.";

        return RedirectToAction(nameof(CertLibrary));
    }

    /// <summary>POST /Certification/AssignRequirement — Assigns a cert to an equipment category.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AssignRequirement(AssignRequirementForm form)
    {
        if (!ModelState.IsValid)
        {
            var vm = BuildCertLibraryViewModel();
            vm.NewRequirement = form;
            return View("CertLibrary", vm);
        }

        _certService.AssignRequirement(form.EquipmentCategory, form.CertTypeId, "Admin");
        TempData["SuccessMessage"] = $"Certification requirement assigned to '{form.EquipmentCategory}'.";
        return RedirectToAction(nameof(CertLibrary));
    }

    /// <summary>POST /Certification/RemoveRequirement/5</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveRequirement(int id)
    {
        var removed = _certService.RemoveRequirement(id);
        TempData[removed ? "SuccessMessage" : "ErrorMessage"] = removed
            ? "Requirement removed."
            : "Requirement not found.";
        return RedirectToAction(nameof(CertLibrary));
    }

    // ── Operator Profile & Document Upload (AC5) ──────────────────────────────

    /// <summary>GET /Certification/OperatorProfile?name=John+Smith</summary>
    public IActionResult OperatorProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return RedirectToAction(nameof(Index));

        _certService.RefreshCertStatuses();
        var model = BuildOperatorProfileViewModel(name);
        return View(model);
    }

    /// <summary>POST /Certification/AddCertRecord</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddCertRecord(AddCertRecordForm form)
    {
        if (!ModelState.IsValid)
        {
            var vm = BuildOperatorProfileViewModel(form.OperatorName);
            vm.NewCertRecord = form;
            return View("OperatorProfile", vm);
        }

        if (form.ExpiryDate <= form.IssuedDate)
        {
            ModelState.AddModelError(nameof(form.ExpiryDate), "Expiry date must be after issued date.");
            var vm = BuildOperatorProfileViewModel(form.OperatorName);
            vm.NewCertRecord = form;
            return View("OperatorProfile", vm);
        }

        _certService.AddCertRecord(form.OperatorName, form.CertTypeId,
            form.IssuedDate, form.ExpiryDate, "Admin",
            notes: form.Notes);

        TempData["SuccessMessage"] = "Certification record added.";
        return RedirectToAction(nameof(OperatorProfile), new { name = form.OperatorName });
    }

    /// <summary>POST /Certification/UploadDocument</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UploadDocument(UploadDocumentForm form)
    {
        if (form.File is null || form.File.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select a file to upload.";
            return RedirectToAction(nameof(OperatorProfile));
        }

        const long maxSize = 10 * 1024 * 1024; // 10 MB
        if (form.File.Length > maxSize)
        {
            TempData["ErrorMessage"] = "File size must not exceed 10 MB.";
            return RedirectToAction(nameof(OperatorProfile));
        }

        var allowed = new[] { "application/pdf", "image/jpeg", "image/png" };
        if (!allowed.Contains(form.File.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Only PDF, JPEG, and PNG files are supported.";
            return RedirectToAction(nameof(OperatorProfile));
        }

        // In MVP, store file name as the stored reference (production would use blob storage)
        var storedName = $"{Guid.NewGuid():N}_{form.File.FileName}";
        _certService.AddDocument(form.CertRecordId, form.File.FileName, form.File.ContentType,
            storedName, form.UploadedBy);

        TempData["SuccessMessage"] = $"Document '{form.File.FileName}' uploaded successfully.";

        // Determine operator name to redirect back
        var certRecord = _certService.GetAllCertRecords()
            .FirstOrDefault(r => r.Id == form.CertRecordId);
        var operatorName = certRecord?.OperatorName ?? string.Empty;

        return RedirectToAction(nameof(OperatorProfile), new { name = operatorName });
    }

    // ── Bulk Import (AC8) ─────────────────────────────────────────────────────

    /// <summary>GET /Certification/BulkImport</summary>
    public IActionResult BulkImport()
    {
        return View(new BulkImportViewModel());
    }

    /// <summary>POST /Certification/BulkImport</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult BulkImport(BulkImportViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var rows = ParseCsv(model.CsvData);
        var (imported, errors) = _certService.BulkImportCertRecords(rows, "Admin");

        model.ImportRan = true;
        model.ImportedCount = imported;
        model.ValidationErrors = errors;

        if (errors.Any())
            TempData["WarningMessage"] = $"{imported} records imported. {errors.Count} rows had validation errors.";
        else
            TempData["SuccessMessage"] = $"{imported} certification records imported successfully.";

        return View(model);
    }

    // ── Override Audit Log (AC2) ──────────────────────────────────────────────

    /// <summary>GET /Certification/OverrideAuditLog</summary>
    public IActionResult OverrideAuditLog()
    {
        var overrides = _certService.GetAllOverrides();
        return View(overrides);
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private CertLibraryViewModel BuildCertLibraryViewModel()
    {
        var certTypes = _certService.GetAllCertTypes();
        var selectList = certTypes.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();

        return new CertLibraryViewModel
        {
            CertTypes = certTypes,
            Requirements = _certService.GetAllRequirements(),
            AllCertTypesForDropdown = certTypes,
            CertTypeSelectList = selectList
        };
    }

    private OperatorProfileViewModel BuildOperatorProfileViewModel(string operatorName)
    {
        var certTypes = _certService.GetAllCertTypes();
        var certTypeById = certTypes.ToDictionary(c => c.Id, c => c);
        var certRecords = _certService.GetCertsForOperator(operatorName);

        var docsByRecord = certRecords.ToDictionary(
            r => r.Id,
            r => _certService.GetDocumentsForCertRecord(r.Id));

        return new OperatorProfileViewModel
        {
            OperatorName = operatorName,
            CertRecords = certRecords,
            CertTypes = certTypes,
            CertTypeById = certTypeById,
            DocumentsByCertRecordId = docsByRecord,
            NewCertRecord = new AddCertRecordForm { OperatorName = operatorName },
            CertTypeSelectList = certTypes.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList()
        };
    }

    /// <summary>
    /// Parses CSV text in the format: OperatorName,CertTypeName,IssuedDate,ExpiryDate[,Notes]
    /// Skips the header row if the first cell equals "OperatorName" (case-insensitive).
    /// </summary>
    private static List<BulkCertImportRow> ParseCsv(string csvData)
    {
        var rows = new List<BulkCertImportRow>();
        if (string.IsNullOrWhiteSpace(csvData)) return rows;

        int rowNumber = 0;
        foreach (var line in csvData.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            rowNumber++;
            var trimmed = line.Trim().Trim('\r');
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            var cols = trimmed.Split(',');

            // Skip header row
            if (rowNumber == 1 &&
                cols[0].Trim().Equals("OperatorName", StringComparison.OrdinalIgnoreCase))
                continue;

            var row = new BulkCertImportRow { RowNumber = rowNumber };

            if (cols.Length >= 1) row.OperatorName = cols[0].Trim();
            if (cols.Length >= 2) row.CertTypeName = cols[1].Trim();
            if (cols.Length >= 3 && DateTime.TryParse(cols[2].Trim(), out var issued))
                row.IssuedDate = issued;
            if (cols.Length >= 4 && DateTime.TryParse(cols[3].Trim(), out var expiry))
                row.ExpiryDate = expiry;
            if (cols.Length >= 5) row.Notes = cols[4].Trim();

            rows.Add(row);
        }

        return rows;
    }
}
