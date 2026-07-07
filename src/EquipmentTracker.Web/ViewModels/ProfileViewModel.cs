using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

public class ProfileViewModel
{
    public string Username { get; set; } = string.Empty;
    public bool IsCoordinator { get; set; }
    public IReadOnlyList<CheckoutHistoryEntry> RecentHistory { get; set; } = [];
}
