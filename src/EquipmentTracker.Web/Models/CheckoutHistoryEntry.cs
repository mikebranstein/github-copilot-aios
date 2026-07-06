namespace EquipmentTracker.Web.Models;

/// <summary>
/// A flattened read-only representation of a checkout record with the
/// resolved item name — used by the Checkout History page.
/// </summary>
public class CheckoutHistoryEntry
{
    public string ItemName { get; init; } = string.Empty;
    public string HolderName { get; init; } = string.Empty;
    public DateTime CheckedOutAtUtc { get; init; }
    public DateTime? ReturnedAtUtc { get; init; }

    public bool IsOpen => ReturnedAtUtc is null;
}
