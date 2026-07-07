using System.Collections.Concurrent;
using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Field Manager bulk checkout and return service.
/// Carts are stored in-memory per user (singleton lifetime).
/// Cart state survives server process restarts only in-process (AC6 offline handling is done
/// via OfflineSyncService for queued transactions; in-app cart persists in memory).
/// Added for Issue #114 — Bulk Checkout and Return Operations for Field Teams.
/// </summary>
public class FieldBulkCheckoutService : IFieldBulkCheckoutService
{
    private readonly IEquipmentService _equipmentService;

    // Separate carts for checkout and return, keyed by userId.
    private readonly ConcurrentDictionary<int, BulkCart> _checkoutCarts = new();
    private readonly ConcurrentDictionary<int, BulkCart> _returnCarts = new();

    public FieldBulkCheckoutService(IEquipmentService equipmentService)
    {
        _equipmentService = equipmentService;
    }

    // ── Checkout Cart ─────────────────────────────────────────────────────────

    public BulkCart GetCheckoutCart(int userId) =>
        _checkoutCarts.GetOrAdd(userId, id => new BulkCart { OwnerUserId = id, CartType = "checkout" });

    public BulkCartItem? AddItemToCheckoutCart(int userId, int itemId)
    {
        var cart = GetCheckoutCart(userId);

        // Duplicate scan guard — idempotent
        if (cart.ContainsItem(itemId))
            return cart.Items.First(i => i.EquipmentItemId == itemId);

        // Cart capacity guard (AC2 constraint: max 50 items)
        if (cart.ItemCount >= BulkCart.MaxItems)
            return null;

        var item = _equipmentService.GetItem(itemId);
        if (item is null)
            return null;

        var cartItem = new BulkCartItem
        {
            EquipmentItemId = item.Id,
            ItemName = item.Name,
            Category = item.Category
        };

        // Conflict detection (TS-2): item already checked out by another user
        if (!item.IsAvailable)
        {
            var activeRecord = _equipmentService.GetActiveCheckoutRecord(itemId);
            cartItem.HasConflict = true;
            cartItem.ConflictHolderName = activeRecord?.BorrowerName ?? "(unknown)";
        }

        cart.Items.Add(cartItem);
        cart.LastActivityUtc = DateTime.UtcNow;

        return cartItem;
    }

    public void RemoveItemFromCheckoutCart(int userId, int itemId)
    {
        if (!_checkoutCarts.TryGetValue(userId, out var cart))
            return;
        cart.Items.RemoveAll(i => i.EquipmentItemId == itemId);
        cart.LastActivityUtc = DateTime.UtcNow;
    }

    public void SkipConflictedItem(int userId, int itemId)
    {
        if (!_checkoutCarts.TryGetValue(userId, out var cart))
            return;
        var item = cart.Items.FirstOrDefault(i => i.EquipmentItemId == itemId);
        if (item is not null)
        {
            item.IsSkipped = true;
            cart.LastActivityUtc = DateTime.UtcNow;
        }
    }

    public void ClearCheckoutCart(int userId)
    {
        if (_checkoutCarts.TryGetValue(userId, out var cart))
        {
            cart.Items.Clear();
            cart.LastActivityUtc = DateTime.UtcNow;
        }
    }

    public BulkOperationResult ConfirmBulkCheckout(int userId, int borrowerUserId, string borrowerName)
    {
        var cart = GetCheckoutCart(userId);
        var batchId = Guid.NewGuid().ToString("N");
        var result = new BulkOperationResult { BatchTransactionId = batchId };

        // Process non-skipped items only; snapshot the list before modifying
        var itemsToProcess = cart.Items.Where(i => !i.IsSkipped).ToList();

        foreach (var cartItem in itemsToProcess)
        {
            // Re-check availability at commit time (item may have been checked out between scan and confirm)
            var equipmentItem = _equipmentService.GetItem(cartItem.EquipmentItemId);
            if (equipmentItem is null || !equipmentItem.IsAvailable)
            {
                result.FailedItems.Add(cartItem);
                continue;
            }

            var success = _equipmentService.Checkout(
                cartItem.EquipmentItemId,
                borrowerName,
                borrowerUserId);

            if (!success)
            {
                result.FailedItems.Add(cartItem);
                continue;
            }

            // Stamp BatchTransactionId on the CheckoutRecord (AC8 — audit trail)
            var record = _equipmentService.GetActiveCheckoutRecord(cartItem.EquipmentItemId);
            if (record is not null)
                record.BatchTransactionId = batchId;

            result.SucceededItems.Add(cartItem);
        }

        // Add skipped items to failed list for reporting
        result.FailedItems.AddRange(cart.Items.Where(i => i.IsSkipped));

        // Clear cart after successful commit
        ClearCheckoutCart(userId);

        return result;
    }

    // ── Return Cart ───────────────────────────────────────────────────────────

    public BulkCart GetReturnCart(int userId) =>
        _returnCarts.GetOrAdd(userId, id => new BulkCart { OwnerUserId = id, CartType = "return" });

    public BulkCartItem? AddItemToReturnCart(int userId, int itemId)
    {
        var cart = GetReturnCart(userId);

        // Duplicate scan guard
        if (cart.ContainsItem(itemId))
            return cart.Items.First(i => i.EquipmentItemId == itemId);

        // Cart capacity guard
        if (cart.ItemCount >= BulkCart.MaxItems)
            return null;

        var item = _equipmentService.GetItem(itemId);
        if (item is null)
            return null;

        // Only items that are currently checked out can be added to a return cart
        if (item.IsAvailable)
            return null;

        var activeRecord = _equipmentService.GetActiveCheckoutRecord(itemId);

        var cartItem = new BulkCartItem
        {
            EquipmentItemId = item.Id,
            ItemName = item.Name,
            Category = item.Category,
            // No conflict concept for returns — but track who holds it for confirmation display
            ConflictHolderName = activeRecord?.BorrowerName
        };

        cart.Items.Add(cartItem);
        cart.LastActivityUtc = DateTime.UtcNow;

        return cartItem;
    }

    public void RemoveItemFromReturnCart(int userId, int itemId)
    {
        if (!_returnCarts.TryGetValue(userId, out var cart))
            return;
        cart.Items.RemoveAll(i => i.EquipmentItemId == itemId);
        cart.LastActivityUtc = DateTime.UtcNow;
    }

    public void ClearReturnCart(int userId)
    {
        if (_returnCarts.TryGetValue(userId, out var cart))
        {
            cart.Items.Clear();
            cart.LastActivityUtc = DateTime.UtcNow;
        }
    }

    public BulkOperationResult ConfirmBulkReturn(int userId)
    {
        var cart = GetReturnCart(userId);
        var batchId = Guid.NewGuid().ToString("N");
        var result = new BulkOperationResult { BatchTransactionId = batchId };

        var itemsToProcess = cart.Items.ToList();

        foreach (var cartItem in itemsToProcess)
        {
            var item = _equipmentService.GetItem(cartItem.EquipmentItemId);
            if (item is null || item.IsAvailable)
            {
                // Item already returned or not found — treat as success (idempotent)
                result.SucceededItems.Add(cartItem);
                continue;
            }

            // Stamp BatchTransactionId on the active record BEFORE return marks it as returned
            var activeRecord = _equipmentService.GetActiveCheckoutRecord(cartItem.EquipmentItemId);
            if (activeRecord is not null)
                activeRecord.BatchTransactionId = batchId;

            var success = _equipmentService.Return(cartItem.EquipmentItemId);
            if (!success)
            {
                result.FailedItems.Add(cartItem);
                continue;
            }

            result.SucceededItems.Add(cartItem);
        }

        // Clear cart after commit
        ClearReturnCart(userId);

        return result;
    }
}
