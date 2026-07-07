using System.ComponentModel.DataAnnotations;

namespace EquipmentTracker.Web.ViewModels;

/// <summary>
/// ViewModel for the manual rental cost entry form (MVP — automated ingestion is P3).
/// </summary>
public class RentalCostEntryViewModel
{
    [Required(ErrorMessage = "Asset category is required.")]
    [Display(Name = "Asset Category")]
    public string AssetCategory { get; set; } = string.Empty;

    [Required(ErrorMessage = "Period start date is required.")]
    [Display(Name = "Period Start")]
    [DataType(DataType.Date)]
    public DateTime PeriodStart { get; set; } = DateTime.UtcNow.AddMonths(-1);

    [Required(ErrorMessage = "Period end date is required.")]
    [Display(Name = "Period End")]
    [DataType(DataType.Date)]
    public DateTime PeriodEnd { get; set; } = DateTime.UtcNow;

    [Required(ErrorMessage = "Cost amount is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Cost must be a positive value.")]
    [Display(Name = "Rental Cost ($)")]
    [DataType(DataType.Currency)]
    public decimal CostAmount { get; set; }

    [Display(Name = "Currency")]
    public string Currency { get; set; } = "USD";
}
