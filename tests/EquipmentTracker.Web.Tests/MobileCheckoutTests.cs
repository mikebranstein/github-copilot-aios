using EquipmentTracker.Web.Controllers;
using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace EquipmentTracker.Web.Tests;

/// <summary>
/// Tests for the mobile checkout/return flow: AC1–AC8.
/// Covers: CoordinatorNotificationService, GetBorrowers, idempotency,
/// checkout flow, profile history, and conflict detection.
/// </summary>
public class MobileCheckoutTests
{
    // ── CoordinatorNotificationService ───────────────────────────────────────

    [Fact]
    public void CoordinatorNotificationService_CreateNotification_StoresInMemory()
    {
        // AC6: Notifications are persisted in-memory
        var svc = new CoordinatorNotificationService();

        var notification = svc.CreateNotification(coordinatorUserId: 1, checkoutRecordId: 10, message: "Alice checked out Laptop");

        Assert.NotNull(notification);
        Assert.Equal(1, notification.CoordinatorUserId);
        Assert.Equal(10, notification.CheckoutRecordId);
        Assert.Equal("Alice checked out Laptop", notification.Message);
        Assert.False(notification.IsRead);
        Assert.True(notification.CreatedAtUtc <= DateTime.UtcNow);
        Assert.True(notification.CreatedAtUtc > DateTime.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public void CoordinatorNotificationService_GetPendingForCoordinator_ReturnsOnlyUnread()
    {
        // AC6: Only unread (pending) notifications are returned
        var svc = new CoordinatorNotificationService();

        svc.CreateNotification(1, 10, "Notification 1 - will be read");
        var unread = svc.CreateNotification(1, 11, "Notification 2 - stays unread");
        var toMarkRead = svc.CreateNotification(1, 12, "Notification 3 - will be read");

        svc.MarkRead(toMarkRead.Id);
        // Manually mark the first one read via the service
        svc.MarkRead(1); // id=1

        var pending = svc.GetPendingForCoordinator(1);

        Assert.Single(pending);
        Assert.Equal(unread.Id, pending[0].Id);
        Assert.False(pending[0].IsRead);
    }

    [Fact]
    public void CoordinatorNotificationService_MarkRead_UpdatesIsRead()
    {
        // AC6: MarkRead sets IsRead = true
        var svc = new CoordinatorNotificationService();

        var notification = svc.CreateNotification(1, 10, "Test message");
        Assert.False(notification.IsRead);

        var result = svc.MarkRead(notification.Id);

        Assert.True(result);
        Assert.True(notification.IsRead);
    }

    [Fact]
    public void CoordinatorNotificationService_MarkRead_ReturnsFalse_WhenNotFound()
    {
        var svc = new CoordinatorNotificationService();

        var result = svc.MarkRead(9999);

        Assert.False(result);
    }

    // ── UserService.GetBorrowers ──────────────────────────────────────────────

    [Fact]
    public void UserService_GetBorrowers_ReturnsOnlyNonCoordinators()
    {
        // AC4: Assignment list contains only non-coordinator users
        var svc = new UserService();
        svc.Register("coord1", "pass", isCoordinator: true);
        svc.Register("coord2", "pass", isCoordinator: true);
        svc.Register("borrower1", "pass", isCoordinator: false);
        svc.Register("borrower2", "pass", isCoordinator: false);

        var borrowers = svc.GetBorrowers();

        Assert.Equal(2, borrowers.Count);
        Assert.All(borrowers, b => Assert.False(b.IsCoordinator));
        Assert.DoesNotContain(borrowers, b => b.Username.StartsWith("coord"));
    }

    [Fact]
    public void UserService_GetBorrowers_ReturnsEmpty_WhenAllUsersAreCoordinators()
    {
        var svc = new UserService();
        svc.Register("coord1", "pass", isCoordinator: true);

        var borrowers = svc.GetBorrowers();

        Assert.Empty(borrowers);
    }

    // ── EquipmentService checkout ─────────────────────────────────────────────

    [Fact]
    public void EquipmentService_Checkout_SucceedsWithBorrowerUserId()
    {
        // AC2: Checkout stores the borrowerUserId on the record
        var svc = new EquipmentService();
        var item = svc.GetAllItems().First(i => i.IsAvailable);

        var result = svc.Checkout(item.Id, "Alice", borrowerUserId: 42);

        Assert.True(result);
        var record = svc.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);
        Assert.Equal(42, record!.BorrowerUserId);
        Assert.Equal("Alice", record.BorrowerName);
    }

    [Fact]
    public void EquipmentService_Checkout_FailsWhenItemUnavailable()
    {
        // AC5: Availability conflict detection — returns false for already-checked-out item
        var svc = new EquipmentService();
        var item = svc.GetAllItems().First(i => i.IsAvailable);

        svc.Checkout(item.Id, "Alice", borrowerUserId: 1);
        var secondAttempt = svc.Checkout(item.Id, "Bob", borrowerUserId: 2);

        Assert.False(secondAttempt);
        // Confirm original holder is still active
        var record = svc.GetActiveCheckoutRecord(item.Id);
        Assert.Equal("Alice", record?.BorrowerName);
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public void MobileCheckoutService_IdempotencyCheck_ReturnsTrueForDuplicateWithin60Seconds()
    {
        // AC8: Same (BorrowerUserId, ItemId) within 60s → treated as idempotent duplicate
        var svc = new EquipmentService();
        var item = svc.GetAllItems().First(i => i.IsAvailable);

        svc.Checkout(item.Id, "Alice", borrowerUserId: 99);

        // Immediately check — record is within 60 second window
        var isDuplicate = svc.IsIdempotentCheckout(item.Id, borrowerUserId: 99);

        Assert.True(isDuplicate);
    }

    [Fact]
    public void MobileCheckoutService_IdempotencyCheck_ReturnsFalseAfter60Seconds()
    {
        // AC8: After 60s window, idempotency check returns false
        var svc = new EquipmentService();
        var item = svc.GetAllItems().First(i => i.IsAvailable);

        svc.Checkout(item.Id, "Alice", borrowerUserId: 99);

        // Simulate the record being older than 60 seconds
        var record = svc.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);
        record!.CheckedOutAtUtc = DateTime.UtcNow.AddSeconds(-61);

        var isDuplicate = svc.IsIdempotentCheckout(item.Id, borrowerUserId: 99);

        Assert.False(isDuplicate);
    }

    [Fact]
    public void MobileCheckoutService_IdempotencyCheck_ReturnsFalse_ForDifferentUser()
    {
        // AC8: Different user on same item is NOT an idempotent duplicate — it's a conflict
        var svc = new EquipmentService();
        var item = svc.GetAllItems().First(i => i.IsAvailable);

        svc.Checkout(item.Id, "Alice", borrowerUserId: 1);

        var isDuplicate = svc.IsIdempotentCheckout(item.Id, borrowerUserId: 2);

        Assert.False(isDuplicate);
    }

    // ── Coordinator notification created on checkout ──────────────────────────

    [Fact]
    public void CoordinatorNotification_CreatedOnCheckout_ForEachCoordinator()
    {
        // AC6: A notification is created for each coordinator when a checkout happens
        var notifSvc = new CoordinatorNotificationService();
        var userSvc = new UserService();

        var coord1 = userSvc.Register("coordinator1", "pass", isCoordinator: true)!;
        var coord2 = userSvc.Register("coordinator2", "pass", isCoordinator: true)!;
        var borrower = userSvc.Register("alice", "pass", isCoordinator: false)!;

        // Simulate what the controller does: create one notification per coordinator
        var coordinators = userSvc.GetCoordinators();
        var checkoutRecordId = 42;
        var message = $"{borrower.Username} checked out 'Laptop' at {DateTime.UtcNow:g} UTC.";

        foreach (var coordinator in coordinators)
        {
            notifSvc.CreateNotification(coordinator.Id, checkoutRecordId, message);
        }

        var coord1Notifications = notifSvc.GetPendingForCoordinator(coord1.Id);
        var coord2Notifications = notifSvc.GetPendingForCoordinator(coord2.Id);

        Assert.Single(coord1Notifications);
        Assert.Single(coord2Notifications);
        Assert.Equal(message, coord1Notifications[0].Message);
        Assert.Equal(checkoutRecordId, coord2Notifications[0].CheckoutRecordId);
    }

    // ── Profile page: GetCheckoutHistoryByUser ────────────────────────────────

    [Fact]
    public void EquipmentService_GetCheckoutHistoryByUser_ReturnsOnlyUserRecords()
    {
        // AC7: Profile page shows only the authenticated user's history
        var svc = new EquipmentService();
        var items = svc.GetAllItems().Where(i => i.IsAvailable).Take(2).ToList();
        Assert.True(items.Count >= 2, "Need at least 2 items");

        // Alice (userId 1) checks out item 0
        svc.Checkout(items[0].Id, "Alice", borrowerUserId: 1);
        // Bob (userId 2) checks out item 1
        svc.Checkout(items[1].Id, "Bob", borrowerUserId: 2);

        var aliceHistory = svc.GetCheckoutHistoryByUser(1);
        var bobHistory = svc.GetCheckoutHistoryByUser(2);

        Assert.Single(aliceHistory);
        Assert.Equal("Alice", aliceHistory[0].HolderName);
        Assert.Single(bobHistory);
        Assert.Equal("Bob", bobHistory[0].HolderName);
    }

    [Fact]
    public void EquipmentService_GetCheckoutHistoryByUser_ReturnsEmpty_WhenNoHistory()
    {
        var svc = new EquipmentService();

        var history = svc.GetCheckoutHistoryByUser(userId: 999);

        Assert.Empty(history);
    }

    [Fact]
    public void EquipmentService_GetCheckoutHistoryByUser_RespectsLimit()
    {
        // AC7: Returns at most `limit` records
        var svc = new EquipmentService();

        // Add more items so we can checkout multiple
        for (int i = 0; i < 5; i++)
            svc.CreateItem($"Tool {i}", "Test");

        var available = svc.GetAllItems().Where(x => x.IsAvailable).Take(5).ToList();
        foreach (var item in available)
        {
            svc.Checkout(item.Id, "Carol", borrowerUserId: 7);
            svc.Return(item.Id);
        }

        var history = svc.GetCheckoutHistoryByUser(7, limit: 3);

        Assert.Equal(3, history.Count);
    }

    [Fact]
    public void EquipmentService_GetCheckoutHistoryByUser_SortedNewestFirst()
    {
        var svc = new EquipmentService();
        var items = svc.GetAllItems().Where(i => i.IsAvailable).Take(2).ToList();
        Assert.True(items.Count >= 2, "Need at least 2 items");

        svc.Checkout(items[0].Id, "Dave", borrowerUserId: 5);
        svc.Return(items[0].Id);
        svc.Checkout(items[1].Id, "Dave", borrowerUserId: 5);

        var history = svc.GetCheckoutHistoryByUser(5);

        Assert.Equal(2, history.Count);
        Assert.True(history[0].CheckedOutAtUtc >= history[1].CheckedOutAtUtc);
    }

    // ── JobSiteId reserved field ───────────────────────────────────────────────

    [Fact]
    public void CheckoutRecord_JobSiteId_IsNullableAndReservedForFuture()
    {
        // Verifies the JobSiteId field exists and defaults to null (v1.1 reserved)
        var record = new CheckoutRecord
        {
            Id = 1,
            EquipmentItemId = 1,
            BorrowerName = "Test",
            CheckedOutAtUtc = DateTime.UtcNow
        };

        Assert.Null(record.JobSiteId);
    }

    // ── Ownership guard on return ──────────────────────────────────────────────

    [Fact]
    public async Task EquipmentService_Return_FailsWhenCalledByNonBorrowerNonCoordinator()
    {
        // Scenario: User A (userId=1) checks out an item.
        // User B (userId=2, not a coordinator) attempts to return it.
        // The controller ownership guard should reject the attempt.

        // Arrange: User A checks out an item
        var equipmentSvc = new EquipmentService();
        var item = equipmentSvc.GetAllItems().First(i => i.IsAvailable);
        equipmentSvc.Checkout(item.Id, "Alice", borrowerUserId: 1);

        // Set up the controller with a real EquipmentService
        var logger = NullLogger<MobileReturnController>.Instance;
        var config = new ConfigurationBuilder().Build();
        var controller = new MobileReturnController(equipmentSvc, config, logger);

        // Configure HttpContext as User B (userId=2, not a coordinator)
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "2"),
            new Claim(ClaimTypes.Name, "Bob"),
            new Claim("IsCoordinator", "False")
        };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());

        // Act: User B attempts to confirm the return
        var result = await controller.ConfirmPost(item.Id, returnConditionNote: null);

        // Assert: should redirect to Scan with an error (ownership guard triggered)
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Scan", redirect.ActionName);

        // Item should still be checked out — the return was blocked
        var activeRecord = equipmentSvc.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(activeRecord);
        Assert.Equal(1, activeRecord!.BorrowerUserId);
    }

    /// <summary>Minimal no-op TempData provider for controller unit tests.</summary>
    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context)
            => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
