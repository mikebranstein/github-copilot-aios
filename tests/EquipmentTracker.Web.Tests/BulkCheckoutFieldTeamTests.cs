using EquipmentTracker.Web.Controllers;
using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Security.Claims;
using Xunit;

namespace EquipmentTracker.Web.Tests;

/// <summary>
/// Tests for the Field Manager Bulk Checkout and Return Operations feature.
/// Issue #114 — Bulk Checkout and Return Operations for Field Teams.
///
/// AC coverage:
///   AC1 — Bulk Checkout: Cart-Style Scan Workflow
///   AC2 — Bulk Checkout: Multi-Item Confirmation and Submission
///   AC3 — Bulk Return: Scan-to-Return Workflow
///   AC5 — Glove-Friendly Mobile UX (model/service constraints)
///   AC6 — Offline-First: BatchTransactionId propagated through OfflineSyncService
///   AC7 — Performance: 10-item checkout completes in well under 5 seconds
///   AC8 — Audit Trail: BatchTransactionId on each CheckoutRecord
///
/// Test scenarios from the requirements supplement:
///   TS-1 — Successful 10-item bulk checkout (happy path)
///   TS-2 — Scanning an already-checked-out item (conflict detection)
///   TS-3 — Offline bulk checkout with sync recovery
///   TS-4 — Cart recovery (cart persists across calls to GetCheckoutCart)
///   TS-5 — Bulk return — partial scan with confirmation
///
/// Run with: dotnet test
/// </summary>
public class BulkCheckoutFieldTeamTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a FieldBulkCheckoutService wired with an EquipmentService seeded with
    /// items 1–3 (Laptop, Projector, Whiteboard Marker Set) and adds extra items up to count.
    /// </summary>
    private static (FieldBulkCheckoutService svc, EquipmentService equipment)
        CreateServices(int extraItems = 0)
    {
        var equipment = new EquipmentService();
        for (int i = 0; i < extraItems; i++)
            equipment.CreateItem($"Field Tool {i + 1}", "Field Equipment");
        var svc = new FieldBulkCheckoutService(equipment);
        return (svc, equipment);
    }

    // ── AC1: Cart-Style Scan Workflow ─────────────────────────────────────────

    [Fact]
    public void AC1_AddItemToCheckoutCart_ItemAppearsInCart()
    {
        // AC1: Item is added to the persistent cart; cart count updates
        var (svc, equipment) = CreateServices();
        var itemId = equipment.GetAllItems().First(i => i.IsAvailable).Id;

        var cartItem = svc.AddItemToCheckoutCart(userId: 10, itemId: itemId);

        Assert.NotNull(cartItem);
        Assert.Equal(itemId, cartItem!.EquipmentItemId);
        Assert.False(cartItem.HasConflict);

        var cart = svc.GetCheckoutCart(userId: 10);
        Assert.Equal(1, cart.ItemCount);
    }

    [Fact]
    public void AC1_AddItem_DuplicateScan_IsIdempotent()
    {
        // AC1: Scanning the same item twice does not duplicate it in the cart
        var (svc, equipment) = CreateServices();
        var itemId = equipment.GetAllItems().First(i => i.IsAvailable).Id;

        svc.AddItemToCheckoutCart(userId: 10, itemId: itemId);
        svc.AddItemToCheckoutCart(userId: 10, itemId: itemId); // second scan

        var cart = svc.GetCheckoutCart(userId: 10);
        Assert.Equal(1, cart.ItemCount);
    }

    [Fact]
    public void AC1_AddItem_UnknownItemId_ReturnsNull()
    {
        // AC1: Scanning an unrecognised barcode returns null (error feedback to user)
        var (svc, _) = CreateServices();

        var cartItem = svc.AddItemToCheckoutCart(userId: 10, itemId: 9999);

        Assert.Null(cartItem);
    }

    [Fact]
    public void AC1_CartPersistedAcrossGetCalls()
    {
        // AC1/TS-4: Cart state survives across multiple GetCheckoutCart calls (in-memory persistence)
        var (svc, equipment) = CreateServices();
        var items = equipment.GetAllItems().Where(i => i.IsAvailable).Take(3).ToList();

        foreach (var item in items)
            svc.AddItemToCheckoutCart(userId: 42, itemId: item.Id);

        // Retrieve cart in a subsequent "call" — same singleton service
        var cart = svc.GetCheckoutCart(userId: 42);

        Assert.Equal(3, cart.ItemCount);
        Assert.Equal(42, cart.OwnerUserId);
    }

    // ── AC2: Multi-Item Confirmation and Submission ───────────────────────────

    /// <summary>TS-1: Successful 10-item bulk checkout (happy path).</summary>
    [Fact]
    public void AC2_TS1_BulkCheckout_10Items_AllCheckedOut_WithSharedBatchId()
    {
        var (svc, equipment) = CreateServices(extraItems: 10);
        var userId = 5;
        var borrowerUserId = 10;

        var available = equipment.GetAllItems().Where(i => i.IsAvailable).Take(10).ToList();
        Assert.Equal(10, available.Count);

        foreach (var item in available)
            svc.AddItemToCheckoutCart(userId, item.Id);

        var result = svc.ConfirmBulkCheckout(userId, borrowerUserId, borrowerName: "FieldManager");

        Assert.Equal(10, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);
        Assert.NotEmpty(result.BatchTransactionId);

        // AC8: Each CheckoutRecord has the shared BatchTransactionId
        foreach (var item in available)
        {
            var record = equipment.GetCheckoutHistory(item.Id).First();
            Assert.Equal(result.BatchTransactionId, record.BatchTransactionId);
        }
    }

    [Fact]
    public void AC2_BulkCheckout_CartClearedAfterConfirm()
    {
        // After confirming, the cart should be empty (ready for the next operation)
        var (svc, equipment) = CreateServices();
        var userId = 5;
        var itemId = equipment.GetAllItems().First(i => i.IsAvailable).Id;

        svc.AddItemToCheckoutCart(userId, itemId);
        svc.ConfirmBulkCheckout(userId, borrowerUserId: 5, borrowerName: "Alice");

        var cart = svc.GetCheckoutCart(userId);
        Assert.Equal(0, cart.ItemCount);
    }

    [Fact]
    public void AC2_BulkCheckout_ItemsUnavailableAfterCheckout()
    {
        // Items checked out in bulk are no longer available
        var (svc, equipment) = CreateServices();
        var userId = 5;
        var items = equipment.GetAllItems().Where(i => i.IsAvailable).Take(2).ToList();

        foreach (var item in items)
            svc.AddItemToCheckoutCart(userId, item.Id);

        svc.ConfirmBulkCheckout(userId, borrowerUserId: 5, borrowerName: "Alice");

        foreach (var item in items)
            Assert.False(equipment.GetItem(item.Id)!.IsAvailable);
    }

    // ── AC3: Bulk Return Scan-to-Return Workflow ──────────────────────────────

    /// <summary>TS-5: Bulk return — partial scan with confirmation.</summary>
    [Fact]
    public void AC3_TS5_BulkReturn_PartialReturn_OnlyScannedItemsReturned()
    {
        var (svc, equipment) = CreateServices(extraItems: 10);
        var userId = 7;

        // Checkout 12 items to user 99
        var items = equipment.GetAllItems().Where(i => i.IsAvailable).Take(12).ToList();
        Assert.Equal(12, items.Count);
        foreach (var item in items)
            equipment.Checkout(item.Id, "FieldWorker", borrowerUserId: 99);

        // Add only 7 to the return cart
        var returnItems = items.Take(7).ToList();
        foreach (var item in returnItems)
            svc.AddItemToReturnCart(userId, item.Id);

        var result = svc.ConfirmBulkReturn(userId);

        Assert.Equal(7, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);

        // The 7 returned items are now available
        foreach (var item in returnItems)
            Assert.True(equipment.GetItem(item.Id)!.IsAvailable);

        // The remaining 5 are still checked out
        foreach (var item in items.Skip(7))
            Assert.False(equipment.GetItem(item.Id)!.IsAvailable);
    }

    [Fact]
    public void AC3_BulkReturn_BatchTransactionId_SharedAcrossReturnedRecords()
    {
        // AC8: All returned records share the same BatchTransactionId
        var (svc, equipment) = CreateServices();
        var userId = 7;

        var items = equipment.GetAllItems().Where(i => i.IsAvailable).Take(3).ToList();
        foreach (var item in items)
            equipment.Checkout(item.Id, "Worker", borrowerUserId: 20);

        foreach (var item in items)
            svc.AddItemToReturnCart(userId, item.Id);

        var result = svc.ConfirmBulkReturn(userId);

        Assert.NotEmpty(result.BatchTransactionId);

        // Each returned record should have the batch ID
        foreach (var item in items)
        {
            var history = equipment.GetCheckoutHistory(item.Id);
            var returned = history.FirstOrDefault();
            // BatchTransactionId is set on the CheckoutRecord
            Assert.Equal(result.BatchTransactionId,
                equipment.GetCheckoutHistory(item.Id)
                    .Select(_ => equipment.GetAllRawCheckoutRecords()
                        .FirstOrDefault(r => r.EquipmentItemId == item.Id && r.ReturnedAtUtc != null))
                    .FirstOrDefault()?.BatchTransactionId);
        }
    }

    [Fact]
    public void AC3_AddItemToReturnCart_AlreadyAvailableItem_ReturnsNull()
    {
        // Cannot add an already-available (not checked out) item to return cart
        var (svc, equipment) = CreateServices();
        var availableItem = equipment.GetAllItems().First(i => i.IsAvailable);

        var cartItem = svc.AddItemToReturnCart(userId: 7, itemId: availableItem.Id);

        Assert.Null(cartItem);
    }

    [Fact]
    public void AC3_AddItemToReturnCart_CheckedOutItem_Succeeds()
    {
        var (svc, equipment) = CreateServices();
        var item = equipment.GetAllItems().First(i => i.IsAvailable);
        equipment.Checkout(item.Id, "Worker", borrowerUserId: 20);

        var cartItem = svc.AddItemToReturnCart(userId: 7, itemId: item.Id);

        Assert.NotNull(cartItem);
        Assert.Equal(item.Id, cartItem!.EquipmentItemId);
        Assert.Equal("Worker", cartItem.ConflictHolderName);
    }

    // ── AC5: Glove-Friendly UX constraints ───────────────────────────────────

    [Fact]
    public void AC5_CartMaxSize_Is50()
    {
        // AC5/AC2: Cart cap of 50 items enforced
        Assert.Equal(50, BulkCart.MaxItems);
    }

    [Fact]
    public void AC5_AddItemToCart_WhenFull_ReturnsNull()
    {
        // AC5: Adding beyond 50 items returns null
        var (svc, equipment) = CreateServices(extraItems: 55);
        var userId = 99;

        var available = equipment.GetAllItems().Where(i => i.IsAvailable).Take(51).ToList();
        Assert.True(available.Count >= 51, "Need at least 51 available items");

        for (int i = 0; i < 50; i++)
            svc.AddItemToCheckoutCart(userId, available[i].Id);

        // The 51st item should be rejected
        var overflow = svc.AddItemToCheckoutCart(userId, available[50].Id);

        Assert.Null(overflow);
        Assert.Equal(50, svc.GetCheckoutCart(userId).ItemCount);
    }

    [Fact]
    public void AC5_CheckoutCartType_IsCheckout()
    {
        var (svc, _) = CreateServices();
        var cart = svc.GetCheckoutCart(userId: 1);
        Assert.Equal("checkout", cart.CartType);
    }

    [Fact]
    public void AC5_ReturnCartType_IsReturn()
    {
        var (svc, _) = CreateServices();
        var cart = svc.GetReturnCart(userId: 1);
        Assert.Equal("return", cart.CartType);
    }

    // ── Conflict detection (TS-2) ─────────────────────────────────────────────

    [Fact]
    public void TS2_ScanningAlreadyCheckedOutItem_MarksConflict_NotBlockingError()
    {
        // TS-2: A conflict indicator appears but does not block adding to cart
        var (svc, equipment) = CreateServices();
        var item = equipment.GetAllItems().First(i => i.IsAvailable);
        equipment.Checkout(item.Id, "OtherUser", borrowerUserId: 50);

        var cartItem = svc.AddItemToCheckoutCart(userId: 10, itemId: item.Id);

        Assert.NotNull(cartItem);
        Assert.True(cartItem!.HasConflict);
        Assert.Equal("OtherUser", cartItem.ConflictHolderName);
    }

    [Fact]
    public void TS2_SkipConflictedItem_ExcludesFromBulkCheckout()
    {
        // TS-2: Field Manager chooses Skip — conflicted item is excluded from checkout
        var (svc, equipment) = CreateServices();
        var items = equipment.GetAllItems().Where(i => i.IsAvailable).Take(2).ToList();

        // Item 0 is available; item 1 is checked out (conflict)
        equipment.Checkout(items[1].Id, "OtherUser", borrowerUserId: 50);

        svc.AddItemToCheckoutCart(userId: 10, itemId: items[0].Id);
        svc.AddItemToCheckoutCart(userId: 10, itemId: items[1].Id); // conflict

        svc.SkipConflictedItem(userId: 10, itemId: items[1].Id);

        var result = svc.ConfirmBulkCheckout(userId: 10, borrowerUserId: 10, borrowerName: "FM");

        Assert.Equal(1, result.SuccessCount);
        // Skipped item appears in failed/skipped list
        Assert.Equal(1, result.FailedCount);
        // Item 0 is now checked out; item 1 is still held by OtherUser
        Assert.False(equipment.GetItem(items[0].Id)!.IsAvailable);
    }

    [Fact]
    public void TS2_ConflictedItemKept_FailsAtCommitTime()
    {
        // TS-2: If Field Manager keeps a conflicted item (does not skip), it fails at commit
        var (svc, equipment) = CreateServices();
        var conflictedItem = equipment.GetAllItems().First(i => i.IsAvailable);
        equipment.Checkout(conflictedItem.Id, "OtherUser", borrowerUserId: 50);

        svc.AddItemToCheckoutCart(userId: 10, itemId: conflictedItem.Id);
        // Do NOT skip — keep in cart

        var result = svc.ConfirmBulkCheckout(userId: 10, borrowerUserId: 10, borrowerName: "FM");

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.FailedCount);
        // Item is still held by OtherUser
        Assert.Equal("OtherUser", equipment.GetCurrentHolder(conflictedItem.Id));
    }

    // ── AC6: Offline BatchTransactionId propagation ───────────────────────────

    [Fact]
    public void AC6_TS3_OfflineBulkCheckout_BatchTransactionId_PreservedOnSync()
    {
        // TS-3/AC6: Offline bulk transactions with BatchTransactionId sync correctly
        var equipment = new EquipmentService();
        var users = new UserService();
        users.Register("coord", "pass", isCoordinator: true);   // id 1
        users.Register("fieldmgr", "pass");                      // id 2
        var notifications = new CoordinatorNotificationService();
        var sync = new OfflineSyncService(equipment, notifications, users);

        var batchId = Guid.NewGuid().ToString("N");
        var items = equipment.GetAllItems().Where(i => i.IsAvailable).Take(3).ToList();

        var transactions = items.Select(item => new OfflineSyncTransaction
        {
            DeviceTransactionId = Guid.NewGuid().ToString(),
            Type = "checkout",
            ItemId = item.Id,
            BorrowerUserId = 2,
            OfflineTimestamp = DateTime.UtcNow.AddMinutes(-5),
            DeviceId = "field-device-1",
            BatchTransactionId = batchId   // all 3 share the same batch ID
        }).ToList();

        var results = sync.ProcessBatch(transactions, requestingUserId: 2);

        Assert.All(results, r => Assert.Equal("success", r.Status));

        // Each checkout record should have the BatchTransactionId preserved
        foreach (var item in items)
        {
            var record = equipment.GetActiveCheckoutRecord(item.Id);
            Assert.NotNull(record);
            Assert.Equal(batchId, record!.BatchTransactionId);
        }
    }

    [Fact]
    public void AC6_TS3_OfflineBulkReturn_BatchTransactionId_PreservedOnSync()
    {
        // AC6/AC8: Offline bulk return preserves BatchTransactionId
        var equipment = new EquipmentService();
        var users = new UserService();
        users.Register("coord", "pass", isCoordinator: true);
        users.Register("fieldmgr", "pass");
        var notifications = new CoordinatorNotificationService();
        var sync = new OfflineSyncService(equipment, notifications, users);

        var items = equipment.GetAllItems().Where(i => i.IsAvailable).Take(2).ToList();
        foreach (var item in items)
            equipment.Checkout(item.Id, "fieldmgr", borrowerUserId: 2);

        var batchId = Guid.NewGuid().ToString("N");
        var returnTransactions = items.Select(item => new OfflineSyncTransaction
        {
            DeviceTransactionId = Guid.NewGuid().ToString(),
            Type = "return",
            ItemId = item.Id,
            BorrowerUserId = 2,
            OfflineTimestamp = DateTime.UtcNow.AddMinutes(-2),
            DeviceId = "field-device-1",
            BatchTransactionId = batchId
        }).ToList();

        var results = sync.ProcessBatch(returnTransactions, requestingUserId: 2);

        Assert.All(results, r => Assert.Equal("success", r.Status));

        // Both returned records should have the BatchTransactionId
        foreach (var item in items)
        {
            var history = equipment.GetCheckoutHistory(item.Id);
            var returnedRecord = history.FirstOrDefault(r => r.ReturnedAtUtc != null);
            Assert.NotNull(returnedRecord);
            Assert.Equal(batchId, returnedRecord!.BatchTransactionId);
        }
    }

    // ── AC7: Performance — 10-item bulk checkout in under 5 seconds ───────────

    [Fact]
    public void AC7_BulkCheckout_10Items_CompletesUnder5Seconds()
    {
        // AC7: The entire transaction (add 10 items + confirm) completes well under 5 seconds
        var (svc, equipment) = CreateServices(extraItems: 10);
        var userId = 11;

        var available = equipment.GetAllItems().Where(i => i.IsAvailable).Take(10).ToList();
        Assert.Equal(10, available.Count);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var item in available)
            svc.AddItemToCheckoutCart(userId, item.Id);

        var result = svc.ConfirmBulkCheckout(userId, borrowerUserId: 11, borrowerName: "SpeedTest");

        sw.Stop();

        Assert.Equal(10, result.SuccessCount);
        Assert.True(sw.Elapsed.TotalSeconds < 5,
            $"Bulk checkout of 10 items took {sw.Elapsed.TotalSeconds:F3}s — expected under 5s");
    }

    // ── AC8: Audit Trail Integrity ────────────────────────────────────────────

    [Fact]
    public void AC8_CheckoutRecord_HasBatchTransactionId_AfterBulkCheckout()
    {
        // AC8: Each individual record carries the batch ID for admin transaction history view
        var (svc, equipment) = CreateServices();
        var userId = 20;
        var items = equipment.GetAllItems().Where(i => i.IsAvailable).Take(2).ToList();

        foreach (var item in items)
            svc.AddItemToCheckoutCart(userId, item.Id);

        var result = svc.ConfirmBulkCheckout(userId, borrowerUserId: 20, borrowerName: "Auditor");

        Assert.NotEmpty(result.BatchTransactionId);

        foreach (var item in items)
        {
            var rawRecord = equipment.GetActiveCheckoutRecord(item.Id);
            Assert.NotNull(rawRecord);
            Assert.Equal(result.BatchTransactionId, rawRecord!.BatchTransactionId);
            Assert.Equal(20, rawRecord.BorrowerUserId);
            Assert.Equal("Auditor", rawRecord.BorrowerName);
            Assert.True(rawRecord.CheckedOutAtUtc > DateTime.UtcNow.AddSeconds(-10));
        }
    }

    [Fact]
    public void AC8_BatchTransactionId_DifferentForEachBulkOperation()
    {
        // AC8: Two separate bulk operations get different batch IDs
        var (svc, equipment) = CreateServices(extraItems: 5);
        var items = equipment.GetAllItems().Where(i => i.IsAvailable).Take(4).ToList();

        // First bulk checkout — items 0 & 1
        svc.AddItemToCheckoutCart(userId: 1, itemId: items[0].Id);
        svc.AddItemToCheckoutCart(userId: 1, itemId: items[1].Id);
        var result1 = svc.ConfirmBulkCheckout(userId: 1, borrowerUserId: 1, borrowerName: "User1");

        // Return them
        equipment.Return(items[0].Id);
        equipment.Return(items[1].Id);

        // Second bulk checkout — same items
        svc.AddItemToCheckoutCart(userId: 1, itemId: items[0].Id);
        svc.AddItemToCheckoutCart(userId: 1, itemId: items[1].Id);
        var result2 = svc.ConfirmBulkCheckout(userId: 1, borrowerUserId: 1, borrowerName: "User1");

        Assert.NotEqual(result1.BatchTransactionId, result2.BatchTransactionId);
    }

    [Fact]
    public void AC8_SingleItemCheckout_NoBatchTransactionId()
    {
        // AC8: Regular single-item checkout (not via bulk service) has null BatchTransactionId
        var equipment = new EquipmentService();
        var item = equipment.GetAllItems().First(i => i.IsAvailable);
        equipment.Checkout(item.Id, "Solo", borrowerUserId: 1);

        var record = equipment.GetActiveCheckoutRecord(item.Id);

        Assert.NotNull(record);
        Assert.Null(record!.BatchTransactionId);
    }

    // ── Cart management ───────────────────────────────────────────────────────

    [Fact]
    public void CartManagement_RemoveItem_RemovesFromCheckoutCart()
    {
        var (svc, equipment) = CreateServices();
        var items = equipment.GetAllItems().Where(i => i.IsAvailable).Take(2).ToList();

        svc.AddItemToCheckoutCart(userId: 5, itemId: items[0].Id);
        svc.AddItemToCheckoutCart(userId: 5, itemId: items[1].Id);
        svc.RemoveItemFromCheckoutCart(userId: 5, itemId: items[0].Id);

        var cart = svc.GetCheckoutCart(userId: 5);
        Assert.Equal(1, cart.ItemCount);
        Assert.DoesNotContain(cart.Items, i => i.EquipmentItemId == items[0].Id);
    }

    [Fact]
    public void CartManagement_ClearCheckoutCart_EmptiesCart()
    {
        var (svc, equipment) = CreateServices();
        var items = equipment.GetAllItems().Where(i => i.IsAvailable).Take(2).ToList();

        foreach (var item in items)
            svc.AddItemToCheckoutCart(userId: 5, itemId: item.Id);

        svc.ClearCheckoutCart(userId: 5);

        Assert.Equal(0, svc.GetCheckoutCart(userId: 5).ItemCount);
    }

    [Fact]
    public void CartManagement_SeparateCartsPerUser()
    {
        // Each user has their own independent cart
        var (svc, equipment) = CreateServices();
        var items = equipment.GetAllItems().Where(i => i.IsAvailable).Take(2).ToList();

        svc.AddItemToCheckoutCart(userId: 1, itemId: items[0].Id);
        svc.AddItemToCheckoutCart(userId: 2, itemId: items[1].Id);

        var cart1 = svc.GetCheckoutCart(userId: 1);
        var cart2 = svc.GetCheckoutCart(userId: 2);

        Assert.Equal(1, cart1.ItemCount);
        Assert.Equal(1, cart2.ItemCount);
        Assert.Equal(items[0].Id, cart1.Items[0].EquipmentItemId);
        Assert.Equal(items[1].Id, cart2.Items[0].EquipmentItemId);
    }

    [Fact]
    public void CartManagement_ReturnCart_RemoveItem_Works()
    {
        var (svc, equipment) = CreateServices();
        var items = equipment.GetAllItems().Where(i => i.IsAvailable).Take(2).ToList();
        foreach (var item in items)
            equipment.Checkout(item.Id, "Worker", borrowerUserId: 30);

        svc.AddItemToReturnCart(userId: 8, itemId: items[0].Id);
        svc.AddItemToReturnCart(userId: 8, itemId: items[1].Id);
        svc.RemoveItemFromReturnCart(userId: 8, itemId: items[0].Id);

        var cart = svc.GetReturnCart(userId: 8);
        Assert.Equal(1, cart.ItemCount);
        Assert.DoesNotContain(cart.Items, i => i.EquipmentItemId == items[0].Id);
    }

    // ── Controller integration tests ──────────────────────────────────────────

    private static FieldBulkCheckoutController BuildCheckoutController(
        IFieldBulkCheckoutService svc,
        IEquipmentService eqSvc,
        IUserService userSvc,
        int userId = 1,
        string username = "fieldmgr")
    {
        var controller = new FieldBulkCheckoutController(svc, eqSvc, userSvc);
        var claims = new[]
        {
            new Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(System.Security.Claims.ClaimTypes.Name, username)
        };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());
        return controller;
    }

    private static FieldBulkReturnController BuildReturnController(
        IFieldBulkCheckoutService svc,
        IEquipmentService eqSvc,
        int userId = 1,
        string username = "fieldmgr")
    {
        var controller = new FieldBulkReturnController(svc, eqSvc);
        var claims = new[]
        {
            new Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(System.Security.Claims.ClaimTypes.Name, username)
        };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());
        return controller;
    }

    [Fact]
    public void Controller_AddItem_ReturnsJson_WithCartCount()
    {
        var equipment = new EquipmentService();
        var userSvc = new UserService();
        var bulkSvc = new FieldBulkCheckoutService(equipment);
        var controller = BuildCheckoutController(bulkSvc, equipment, userSvc, userId: 1);

        var itemId = equipment.GetAllItems().First(i => i.IsAvailable).Id;
        var result = controller.AddItem(itemId);

        var json = Assert.IsType<JsonResult>(result);
        Assert.NotNull(json.Value);
    }

    [Fact]
    public void Controller_AddItem_UnknownItem_Returns404()
    {
        var equipment = new EquipmentService();
        var userSvc = new UserService();
        var bulkSvc = new FieldBulkCheckoutService(equipment);
        var controller = BuildCheckoutController(bulkSvc, equipment, userSvc, userId: 1);

        var result = controller.AddItem(9999);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFound.Value);
    }

    [Fact]
    public void Controller_Confirm_EmptyCart_RedirectsToScan()
    {
        var equipment = new EquipmentService();
        var userSvc = new UserService();
        userSvc.Register("fieldmgr", "pass");
        var bulkSvc = new FieldBulkCheckoutService(equipment);
        var controller = BuildCheckoutController(bulkSvc, equipment, userSvc, userId: 1);

        var result = controller.Confirm(borrowerUserId: 1, borrowerName: "fieldmgr");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Scan", redirect.ActionName);
    }

    [Fact]
    public void Controller_ReturnAddItem_CheckedOutItem_ReturnsJson()
    {
        var equipment = new EquipmentService();
        var item = equipment.GetAllItems().First(i => i.IsAvailable);
        equipment.Checkout(item.Id, "worker", borrowerUserId: 5);

        var bulkSvc = new FieldBulkCheckoutService(equipment);
        var controller = BuildReturnController(bulkSvc, equipment, userId: 1);

        var result = controller.AddItem(item.Id);

        var json = Assert.IsType<JsonResult>(result);
        Assert.NotNull(json.Value);
    }

    [Fact]
    public void Controller_ReturnConfirm_EmptyCart_RedirectsToScan()
    {
        var equipment = new EquipmentService();
        var bulkSvc = new FieldBulkCheckoutService(equipment);
        var controller = BuildReturnController(bulkSvc, equipment, userId: 1);

        var result = controller.Confirm();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Scan", redirect.ActionName);
    }

    /// <summary>Minimal no-op TempData provider for controller unit tests.</summary>
    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context)
            => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
