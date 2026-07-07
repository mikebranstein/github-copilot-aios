using EquipmentTracker.Web.Models;
using ModelStatus = EquipmentTracker.Web.Models.EquipmentStatus;

namespace EquipmentTracker.Web.ViewModels;

public class EquipmentListItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool IsAvailable { get; init; }
    public bool IsOverdue { get; init; }
    public string? BorrowerName { get; init; }
    public int DaysCheckedOut { get; init; }
    public ModelStatus Status { get; init; }
    public string? SiteName { get; init; }
    public string StatusBadgeClass => Status switch
    {
        ModelStatus.Available => "bg-success",
        ModelStatus.InUse => "bg-warning text-dark",
        ModelStatus.Reserved => "bg-info text-dark",
        ModelStatus.Maintenance => "bg-secondary",
        _ => "bg-light text-dark"
    };
    public string StatusDisplay => Status switch
    {
        ModelStatus.Available => "Available",
        ModelStatus.InUse => "In Use",
        ModelStatus.Reserved => "Reserved",
        ModelStatus.Maintenance => "Maintenance",
        _ => Status.ToString()
    };
}

public class EquipmentListViewModel
{
    public IReadOnlyList<EquipmentListItemViewModel> Items { get; init; } = [];
    public int AvailableCount { get; init; }
}
