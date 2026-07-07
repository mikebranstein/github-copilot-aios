using EquipmentTracker.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// Coordinator-only audit trail CSV export.
/// Routes: /audit/export/*
/// </summary>
[Authorize]
[Route("audit/export")]
public class AuditExportController : Controller
{
    private readonly IAuditExportService _auditExportService;

    public AuditExportController(IAuditExportService auditExportService)
    {
        _auditExportService = auditExportService;
    }

    // GET /audit/export
    [HttpGet("")]
    public IActionResult Index()
    {
        var isCoordinator = bool.TryParse(User.FindFirstValue("IsCoordinator"), out var cv) && cv;
        if (!isCoordinator) return Forbid();

        return View();
    }

    // GET /audit/export/csv?from={}&to={}
    [HttpGet("csv")]
    public IActionResult ExportCsv([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var isCoordinator = bool.TryParse(User.FindFirstValue("IsCoordinator"), out var cv) && cv;
        if (!isCoordinator) return Forbid();

        if (from is null || to is null)
        {
            TempData["ErrorMessage"] = "Please provide both from and to dates.";
            return RedirectToAction("Index");
        }

        string csv;
        try
        {
            csv = _auditExportService.GenerateCsv(from.Value, to.Value);
        }
        catch (ArgumentException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction("Index");
        }

        var filename = $"audit-{from.Value:yyyyMMdd}-{to.Value:yyyyMMdd}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", filename);
    }
}
