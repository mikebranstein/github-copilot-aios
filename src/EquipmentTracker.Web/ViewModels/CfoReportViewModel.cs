using EquipmentTracker.Web.Services;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>
/// ViewModel for the CFO Executive Report view/export (AC-5, AC-6, AC-7).
/// </summary>
public class CfoReportViewModel
{
    public CfoReportData? ReportData { get; set; }

    /// <summary>Whether the current user is authorized to view this report.</summary>
    public bool IsAuthorized { get; set; }

    /// <summary>
    /// Tooltip shown to unauthorized users explaining why the export button is unavailable.
    /// </summary>
    public string UnauthorizedTooltip { get; set; } =
        "Available to Admins and Finance roles only.";
}
