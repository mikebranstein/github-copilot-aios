using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>Per-item checkout history view model (used on the Equipment Detail page).</summary>
public class ItemCheckoutHistoryViewModel
{
    public int EquipmentItemId { get; set; }
    public string EquipmentItemName { get; set; } = string.Empty;
    public IReadOnlyList<CheckoutRecord> History { get; set; } = Array.Empty<CheckoutRecord>();
}

public class CheckoutHistoryRowViewModel
{
    public string ItemName { get; init; } = string.Empty;
    public string HolderName { get; init; } = string.Empty;
    public DateTime CheckedOutAtUtc { get; init; }
    public DateTime? ReturnedAtUtc { get; init; }
    public bool IsOpen { get; init; }

    /// <summary>
    /// "Currently checked out" for open checkouts; formatted return date for closed ones.
    /// </summary>
    public string ReturnDateDisplay { get; init; } = string.Empty;

    /// <summary>
    /// "Ongoing — N days" for open checkouts; "N days" for closed ones.
    /// </summary>
    public string DurationDisplay { get; init; } = string.Empty;
}

public class CheckoutHistoryViewModel
{
    public IReadOnlyList<CheckoutHistoryRowViewModel> Rows { get; init; } = Array.Empty<CheckoutHistoryRowViewModel>();
    public int CurrentPage { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public int TotalMatchingCount { get; init; }
    public string? Filter { get; init; }

    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
}
