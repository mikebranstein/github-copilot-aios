using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Field Manager bulk checkout and return service.
/// Manages per-user in-memory carts, conflict detection, and batch commit.
/// Added for Issue #114 — Bulk Checkout and Return Operations for Field Teams.
/// </summary>
public interface IFieldBulkCheckoutService
{
    // ── Checkout Cart ─────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the current checkout cart for the given user, creating an empty one if none exists.
    /// </summary>
    BulkCart GetCheckoutCart(int userId);

    /// <summary>
    /// Adds an equipment item to the user's checkout cart by item ID or name/barcode code.
    /// Returns the updated cart item (including conflict info if item is already checked out).
    /// Returns null if the item is not found or the cart is full (50 items max).
    /// Does not add duplicate items (idempotent scan).
    /// </summary>
    BulkCartItem? AddItemToCheckoutCart(int userId, int itemId);

    /// <summary>
    /// Removes an item from the checkout cart.
    /// </summary>
    void RemoveItemFromCheckoutCart(int userId, int itemId);

    /// <summary>
    /// Marks a conflicted cart item as skipped (will not be checked out on confirm).
    /// </summary>
    void SkipConflictedItem(int userId, int itemId);

    /// <summary>
    /// Clears all items from the checkout cart.
    /// </summary>
    void ClearCheckoutCart(int userId);

    /// <summary>
    /// Confirms the bulk checkout. Checks out all non-skipped items simultaneously,
    /// assigns a shared BatchTransactionId to each CheckoutRecord.
    /// Items already checked out (conflict, not skipped) are skipped automatically.
    /// Returns the operation result with per-item status.
    /// </summary>
    BulkOperationResult ConfirmBulkCheckout(int userId, int borrowerUserId, string borrowerName);

    // ── Return Cart ───────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the current return cart for the given user.
    /// </summary>
    BulkCart GetReturnCart(int userId);

    /// <summary>
    /// Adds an equipment item to the user's return cart.
    /// Returns null if item not found, already returned/available, cart full, or duplicate scan.
    /// </summary>
    BulkCartItem? AddItemToReturnCart(int userId, int itemId);

    /// <summary>
    /// Removes an item from the return cart.
    /// </summary>
    void RemoveItemFromReturnCart(int userId, int itemId);

    /// <summary>
    /// Clears the return cart.
    /// </summary>
    void ClearReturnCart(int userId);

    /// <summary>
    /// Confirms the bulk return. Returns all items in the return cart simultaneously,
    /// assigning a shared BatchTransactionId to each returned CheckoutRecord.
    /// </summary>
    BulkOperationResult ConfirmBulkReturn(int userId);
}
