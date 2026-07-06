using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

public class CheckoutHistoryViewModel
{
    public int EquipmentItemId { get; set; }
    public string EquipmentItemName { get; set; } = string.Empty;
    public IReadOnlyList<CheckoutRecord> History { get; set; } = Array.Empty<CheckoutRecord>();
}
