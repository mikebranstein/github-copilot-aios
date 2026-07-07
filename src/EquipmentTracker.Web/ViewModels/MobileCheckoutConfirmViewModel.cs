using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

public class MobileCheckoutConfirmViewModel
{
    public EquipmentItem Item { get; set; } = null!;
    /// <summary>Pre-selected assignee user ID (0 = assign to self).</summary>
    public int AssigneeId { get; set; }
    public IReadOnlyList<ApplicationUser> Borrowers { get; set; } = [];
    public string? ErrorMessage { get; set; }
}
