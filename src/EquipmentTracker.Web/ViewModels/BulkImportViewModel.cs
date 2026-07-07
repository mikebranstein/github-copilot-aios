using EquipmentTracker.Web.Models;
using System.ComponentModel.DataAnnotations;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>ViewModel for the bulk CSV cert import page (AC8).</summary>
public class BulkImportViewModel
{
    /// <summary>Raw CSV text pasted or uploaded by the administrator.</summary>
    [Required(ErrorMessage = "CSV data is required.")]
    public string CsvData { get; set; } = string.Empty;

    // ── Results (populated after import) ──────────────────────────────────────

    public bool ImportRan { get; set; } = false;
    public int ImportedCount { get; set; }
    public List<(int Row, string Error)> ValidationErrors { get; set; } = [];

    public bool HasErrors => ValidationErrors.Any();
}
