using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>Per-operator row in the compliance dashboard.</summary>
public class OperatorComplianceRow
{
    /// <summary>Operator's full name.</summary>
    public string OperatorName { get; set; } = string.Empty;

    /// <summary>All certification records for this operator.</summary>
    public IReadOnlyList<CertComplianceEntry> Certs { get; set; } = [];

    /// <summary>Worst status across all certs: RED > YELLOW > GREEN.</summary>
    public ComplianceColor OverallColor { get; set; }
}

public class CertComplianceEntry
{
    public int RecordId { get; set; }
    public string CertTypeName { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public CertificationStatus Status { get; set; }

    /// <summary>Traffic-light color for this cert.</summary>
    public ComplianceColor Color => Status switch
    {
        CertificationStatus.Expired => ComplianceColor.Red,
        CertificationStatus.ExpiringSoon => ComplianceColor.Yellow,
        _ => ComplianceColor.Green
    };

    public string ColorClass => Color switch
    {
        ComplianceColor.Red => "table-danger",
        ComplianceColor.Yellow => "table-warning",
        _ => "table-success"
    };

    public string BadgeClass => Color switch
    {
        ComplianceColor.Red => "bg-danger",
        ComplianceColor.Yellow => "bg-warning text-dark",
        _ => "bg-success"
    };

    public string StatusLabel => Status switch
    {
        CertificationStatus.Expired => "Expired",
        CertificationStatus.ExpiringSoon => "Expiring Soon",
        CertificationStatus.Missing => "Missing",
        _ => "Compliant"
    };
}

/// <summary>Traffic-light color for the compliance dashboard.</summary>
public enum ComplianceColor { Green, Yellow, Red }

/// <summary>ViewModel for the color-coded compliance dashboard (AC7).</summary>
public class ComplianceDashboardViewModel
{
    public IReadOnlyList<OperatorComplianceRow> Operators { get; set; } = [];
    public int TotalOperators { get; set; }
    public int RedCount { get; set; }
    public int YellowCount { get; set; }
    public int GreenCount { get; set; }
}
