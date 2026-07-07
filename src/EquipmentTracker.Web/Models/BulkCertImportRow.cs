namespace EquipmentTracker.Web.Models;

/// <summary>
/// A single row from a bulk CSV/Excel certification import.
/// Parsed by the controller before being passed to <see cref="Services.ICertificationService.BulkImportCertRecords"/>.
/// </summary>
public class BulkCertImportRow
{
    /// <summary>1-based row number in the source file (for error reporting).</summary>
    public int RowNumber { get; set; }

    public string OperatorName { get; set; } = string.Empty;
    public string CertTypeName { get; set; } = string.Empty;

    /// <summary>Parsed issued date.</summary>
    public DateTime IssuedDate { get; set; }

    /// <summary>Parsed expiry date.</summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>Optional notes column.</summary>
    public string? Notes { get; set; }
}
