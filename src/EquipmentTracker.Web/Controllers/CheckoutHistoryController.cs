using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Web.Controllers;

public class CheckoutHistoryController : Controller
{
    private const int PageSize = 25;
    private readonly IEquipmentService _equipmentService;

    public CheckoutHistoryController(IEquipmentService equipmentService)
    {
        _equipmentService = equipmentService;
    }

    // GET /CheckoutHistory
    public IActionResult Index(string? filter, int page = 1)
    {
        var allHistory = _equipmentService.GetAllCheckoutHistory();

        // Apply case-insensitive holder-name filter
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? allHistory
            : allHistory
                .Where(e => e.HolderName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

        int totalMatchingCount = filtered.Count;
        int totalPages = Math.Max(1, (int)Math.Ceiling(totalMatchingCount / (double)PageSize));

        // Clamp page to valid range
        page = Math.Clamp(page, 1, totalPages);

        var pageRows = filtered
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(e => BuildRow(e))
            .ToList();

        var model = new CheckoutHistoryViewModel
        {
            Rows = pageRows,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalMatchingCount = totalMatchingCount,
            Filter = filter
        };

        return View(model);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private static CheckoutHistoryRowViewModel BuildRow(Models.CheckoutHistoryEntry e)
    {
        bool isOpen = e.IsOpen;
        DateTime now = DateTime.UtcNow;

        string returnDateDisplay;
        string durationDisplay;

        if (isOpen)
        {
            returnDateDisplay = "Currently checked out";
            int days = (int)(now - e.CheckedOutAtUtc).TotalDays;
            durationDisplay = $"Ongoing \u2014 {days} day{(days == 1 ? "" : "s")}";
        }
        else
        {
            returnDateDisplay = e.ReturnedAtUtc!.Value.ToLocalTime().ToString("yyyy-MM-dd");
            int days = (int)(e.ReturnedAtUtc!.Value - e.CheckedOutAtUtc).TotalDays;
            durationDisplay = $"{days} day{(days == 1 ? "" : "s")}";
        }

        return new CheckoutHistoryRowViewModel
        {
            ItemName = e.ItemName,
            HolderName = e.HolderName,
            CheckedOutAtUtc = e.CheckedOutAtUtc,
            ReturnedAtUtc = e.ReturnedAtUtc,
            IsOpen = isOpen,
            ReturnDateDisplay = returnDateDisplay,
            DurationDisplay = durationDisplay
        };
    }
}
