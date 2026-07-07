using System.Text;
using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-memory audit export service. Generates CSV reports of checkout history.
/// </summary>
public class AuditExportService : IAuditExportService
{
    private readonly IEquipmentService _equipmentService;
    private readonly IApprovalService _approvalService;

    public AuditExportService(IEquipmentService equipmentService, IApprovalService approvalService)
    {
        _equipmentService = equipmentService;
        _approvalService = approvalService;
    }

    public string GenerateCsv(DateTime from, DateTime to)
    {
        if ((to - from).TotalDays > 90)
            throw new ArgumentException("Date range must be 90 days or less.");

        var allRecords = _equipmentService.GetAllRawCheckoutRecords();
        var allApprovals = _approvalService.GetAll();
        var approvalByRecordId = allApprovals.ToDictionary(a => a.CheckoutRecordId);

        // Build item name lookup
        var itemNameById = _equipmentService.GetAllItems().ToDictionary(i => i.Id, i => i.Name);

        var filtered = allRecords
            .Where(r => r.CheckedOutAtUtc >= from && r.CheckedOutAtUtc <= to)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("EquipmentId,ItemName,CheckedOutBy,CheckoutTimestamp,ReturnTimestamp,ApprovalStatus,DenialReason,ConditionNote,ReturnConditionNote");

        foreach (var record in filtered)
        {
            approvalByRecordId.TryGetValue(record.Id, out var approval);

            var itemName = itemNameById.TryGetValue(record.EquipmentItemId, out var n) ? n : "(unknown)";
            var approvalStatus = approval?.Status.ToString() ?? string.Empty;
            var denialReason = approval?.DenialReason ?? string.Empty;

            sb.AppendLine(string.Join(",",
                record.EquipmentItemId,
                CsvEscape(itemName),
                CsvEscape(record.BorrowerName),
                record.CheckedOutAtUtc.ToString("o"),
                record.ReturnedAtUtc?.ToString("o") ?? string.Empty,
                CsvEscape(approvalStatus),
                CsvEscape(denialReason),
                CsvEscape(record.ConditionNote ?? string.Empty),
                CsvEscape(record.ReturnConditionNote ?? string.Empty)
            ));
        }

        return sb.ToString();
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
