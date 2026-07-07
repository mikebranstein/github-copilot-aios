namespace EquipmentTracker.Web.ViewModels;

public enum EquipmentStatus
{
    Available,
    CheckedOut,
    Overdue
}

public class CatalogItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public EquipmentStatus Status { get; set; }
}
