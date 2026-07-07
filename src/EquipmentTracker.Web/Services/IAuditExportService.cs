namespace EquipmentTracker.Web.Services;

public interface IAuditExportService
{
    /// <summary>
    /// Generates a CSV string of all checkout records within [from, to].
    /// Throws ArgumentException if the range exceeds 90 days.
    /// </summary>
    string GenerateCsv(DateTime from, DateTime to);
}
