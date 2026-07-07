using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

public class MobileReturnConfirmViewModel
{
    public EquipmentItem Item { get; set; } = null!;
    public CheckoutRecord? ActiveRecord { get; set; }
    public string? ErrorMessage { get; set; }
}
