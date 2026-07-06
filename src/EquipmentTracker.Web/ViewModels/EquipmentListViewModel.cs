using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>Per-row data for the equipment list, including overdue status.</summary>
public class EquipmentListItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool IsAvailable { get; init; }

    /// <summary>True when the item is checked out and has exceeded the overdue threshold.</summary>
    public bool IsOverdue { get; init; }

    /// <summary>Borrower name from the active checkout record; null when available.</summary>
    public string? BorrowerName { get; init; }

    /// <summary>Number of whole days the item has been checked out; 0 when available.</summary>
    public int DaysCheckedOut { get; init; }
}

public class EquipmentListViewModel
{
    public IReadOnlyList<EquipmentListItemViewModel> Items { get; init; } = [];
    public int AvailableCount { get; init; }
}
