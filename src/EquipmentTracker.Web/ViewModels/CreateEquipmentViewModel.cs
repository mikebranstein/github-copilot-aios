using System.ComponentModel.DataAnnotations;

namespace EquipmentTracker.Web.ViewModels;

public class CreateEquipmentViewModel
{
    [Required(ErrorMessage = "Item name is required.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string Category { get; set; } = string.Empty;
}
