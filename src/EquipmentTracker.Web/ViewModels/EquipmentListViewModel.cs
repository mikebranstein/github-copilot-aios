using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

public class EquipmentListViewModel
{
    public IReadOnlyList<EquipmentItem> Items { get; init; } = [];
    public int AvailableCount { get; init; }
}
