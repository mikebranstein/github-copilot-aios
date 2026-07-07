namespace EquipmentTracker.Web.ViewModels;

public class EquipmentDetailViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? BorrowerName { get; set; }
    public DateTime? CheckedOutAtUtc { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public EquipmentStatus Status { get; set; }
}
