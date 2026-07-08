using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public interface IEquipmentService
{
    IReadOnlyList<EquipmentItem> GetAllItems();
    EquipmentItem? GetItem(int id);
    EquipmentItem CreateItem(string name, string category);

    /// <summary>Returns false if the item does not exist or is already checked out.</summary>
    bool Checkout(int itemId, string borrowerName, int? borrowerUserId = null, string? conditionNote = null, int? bulkCheckoutInitiatorId = null, int? newSiteId = null);

    /// <summary>Returns false if the item does not exist or is already available.</summary>
    bool Return(int itemId, string? returnConditionNote = null);

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

    /// <summary>
    /// Returns checkout history for the specified user (by BorrowerUserId), newest-first.
    /// Returns up to the requested limit (default 30). Returns an empty list if user has no history.
    /// </summary>
    IReadOnlyList<CheckoutHistoryEntry> GetCheckoutHistoryByUser(int userId, int limit = 30);

    /// <summary>Returns a checkout record by its ID across all items, or null if not found.</summary>
    CheckoutRecord? GetCheckoutRecordById(int recordId);

    /// <summary>Returns all raw checkout records across all items (for audit export).</summary>
    IReadOnlyList<CheckoutRecord> GetAllRawCheckoutRecords();

    /// <summary>
    /// Returns true if a non-returned checkout for the same (BorrowerUserId, EquipmentItemId)
    /// pair was created within the last 60 seconds. Used for server-side idempotency.
    /// </summary>
    bool IsIdempotentCheckout(int itemId, int borrowerUserId);

    IReadOnlyList<EquipmentItem> GetItemsBySite(int? siteId);
    IReadOnlyList<EquipmentItem> GetItemsByStatus(EquipmentStatus status);
    bool UpdateItemSite(int itemId, int? siteId);
    bool UpdateItemStatus(int itemId, EquipmentStatus status);

    // Added for Issue #121 — Offline-First Mobile App for Field Workers

    /// <summary>
    /// Flags an equipment item as damaged/conditioned. Sets IsFlagged = true on the item.
    /// Returns the created DamageFlag, or null if the item does not exist.
    /// </summary>
    DamageFlag? FlagDamage(int itemId, string description, int? reportedByUserId, string deviceTransactionId, DateTime deviceTimestamp);

    /// <summary>
    /// Returns all damage flags for the given equipment item, newest first.
    /// </summary>
    IReadOnlyList<DamageFlag> GetDamageFlags(int itemId);

    /// <summary>
    /// Returns all active (unresolved) damage flags across all items.
    /// </summary>
    IReadOnlyList<DamageFlag> GetAllActiveDamageFlags();

    /// <summary>
    /// Clears the damage flag on the given item (marks it as resolved).
    /// Returns false if the item does not exist or is not flagged.
    /// </summary>
    bool ClearDamageFlag(int itemId);

    /// <summary>
    /// Returns the active damage flag (first unresolved) for an item, or null if none.
    /// </summary>
    DamageFlag? GetActiveDamageFlag(int itemId);
}
