namespace EquipmentTracker.Web.ViewModels;

/// <summary>
/// View model for the coordinator side-by-side photo comparison screen.
/// Added for Issue #58 — Photo-Backed Checkout &amp; Return.
/// </summary>
public class CoordinatorPhotoReviewViewModel
{
    public int CheckoutRecordId { get; set; }
    public string BorrowerName { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public DateTime CheckedOutAtUtc { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }

    // AC-CR1: Both photos
    public string? CheckoutPhotoUrl { get; set; }
    public string? ReturnPhotoUrl { get; set; }

    // AC-CR2: Enlarge commands (represented as bool flags for testability)
    public bool EnlargeCheckoutPhotoCommand { get; set; } = true;
    public bool EnlargeReturnPhotoCommand { get; set; } = true;

    // AC-CR3: Damage assessment
    public string ConditionAssessment { get; set; } = "NoDamage";

    public bool HasCheckoutPhoto => !string.IsNullOrEmpty(CheckoutPhotoUrl);
    public bool HasReturnPhoto => !string.IsNullOrEmpty(ReturnPhotoUrl);
}
