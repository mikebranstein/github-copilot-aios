using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public interface IEquipmentService
{
    IReadOnlyList<EquipmentItem> GetAllItems();
    EquipmentItem? GetItem(int id);
    EquipmentItem CreateItem(string name, string category);

    /// <summary>Returns false if the item does not exist or is already checked out.</summary>
    bool Checkout(int itemId, string borrowerName);

    /// <summary>Returns false if the item does not exist or is already available.</summary>
    bool Return(int itemId);

    /// <summary>Returns the borrower name of the active (unreturned) checkout for the given item, or null if the item is available.</summary>
    string? GetCurrentHolder(int itemId);

    /// <summary>Returns checkout records for the given item in reverse chronological order (newest first). Returns an empty list if the item has no history.</summary>
    IReadOnlyList<CheckoutRecord> GetCheckoutHistory(int itemId);

    /// <summary>
    /// Returns the active (not yet returned) checkout record for the given item,
    /// or null if the item is currently available or does not exist.
    /// </summary>
    CheckoutRecord? GetActiveCheckoutRecord(int itemId);

    /// <summary>
    /// Returns all checkout records across all items, sorted newest-first (by CheckedOutAtUtc descending).
    /// Each entry includes the resolved item name.
    /// </summary>
    IReadOnlyList<CheckoutHistoryEntry> GetAllCheckoutHistory();
}
