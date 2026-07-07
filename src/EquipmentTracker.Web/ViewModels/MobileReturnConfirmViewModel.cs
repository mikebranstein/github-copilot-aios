using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.ViewModels;

public class MobileReturnConfirmViewModel
{
    public EquipmentItem Item { get; set; } = null!;
    public CheckoutRecord? ActiveRecord { get; set; }
    public string? ErrorMessage { get; set; }
    /// <summary>When true, the return condition note is required.</summary>
    public bool ConditionCaptureRequired { get; set; }

    // Fair Witness fields — AC-DM3
    public string? FairWitnessPhotoUrl { get; set; }
    public DateTime? FairWitnessTimestamp { get; set; }
    public string? FairWitnessItemName { get; set; }
    public bool HasFairWitnessPhoto => !string.IsNullOrEmpty(FairWitnessPhotoUrl);

    // Return photo capture state — AC-R1
    public bool IsCaptureReturnPhotoButtonEnabled { get; set; } = true;

    // Return photo — AC-R2
    public string? ConditionPhotoAtReturn { get; set; }
}
