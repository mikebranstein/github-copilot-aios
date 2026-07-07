using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using Xunit;

namespace EquipmentTracker.Web.Tests;

/// <summary>
/// Tests for OfflineSyncService.ProcessBatch() and supporting functionality.
/// AC7: All tests operate purely in-memory (no I/O) and complete well within 30 s.
/// </summary>
public class OfflineSyncTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a fully wired-up OfflineSyncService with an in-memory EquipmentService
    /// seeded with items (IDs 1–3) and two users:
    ///   - ID 1: coordinator ("coord")
    ///   - ID 2: borrower   ("alice")
    ///   - ID 3: borrower   ("bob")
    /// </summary>
    private static (OfflineSyncService sync, EquipmentService equipment, UserService users)
        CreateServices()
    {
        var equipment = new EquipmentService();           // seeds items 1–3
        var users     = new UserService();
        users.Register("coord", "pass", isCoordinator: true);  // id 1
        users.Register("alice", "pass");                         // id 2
        users.Register("bob",   "pass");                         // id 3

        var notifications = new CoordinatorNotificationService();
        var sync = new OfflineSyncService(equipment, notifications, users);
        return (sync, equipment, users);
    }

    private static OfflineSyncTransaction MakeCheckout(
        int itemId,
        int borrowerUserId,
        DateTime? ts = null,
        string? id = null) =>
        new()
        {
            DeviceTransactionId = id ?? Guid.NewGuid().ToString(),
            Type                = "checkout",
            ItemId              = itemId,
            BorrowerUserId      = borrowerUserId,
            OfflineTimestamp    = ts ?? DateTime.UtcNow,
            DeviceId            = "test-device"
        };

    private static OfflineSyncTransaction MakeReturn(
        int itemId,
        int borrowerUserId,
        DateTime? ts = null,
        string? id = null) =>
        new()
        {
            DeviceTransactionId = id ?? Guid.NewGuid().ToString(),
            Type                = "return",
            ItemId              = itemId,
            BorrowerUserId      = borrowerUserId,
            OfflineTimestamp    = ts ?? DateTime.UtcNow,
            DeviceId            = "test-device"
        };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void OfflineSyncService_ProcessBatch_SuccessfulCheckout()
    {
        // Arrange
        var (sync, equipment, _) = CreateServices();
        var tx = MakeCheckout(itemId: 1, borrowerUserId: 2);

        // Act
        var results = sync.ProcessBatch(new[] { tx }, requestingUserId: 2);

        // Assert
        Assert.Single(results);
        Assert.Equal("success", results[0].Status);
        Assert.Equal(tx.DeviceTransactionId, results[0].DeviceTransactionId);
        Assert.False(equipment.GetItem(1)!.IsAvailable);
    }

    [Fact]
    public void OfflineSyncService_ProcessBatch_SuccessfulReturn()
    {
        // Arrange
        var (sync, equipment, _) = CreateServices();
        equipment.Checkout(1, "alice", borrowerUserId: 2);

        var tx = MakeReturn(itemId: 1, borrowerUserId: 2);

        // Act
        var results = sync.ProcessBatch(new[] { tx }, requestingUserId: 2);

        // Assert
        Assert.Single(results);
        Assert.Equal("success", results[0].Status);
        Assert.True(equipment.GetItem(1)!.IsAvailable);
    }

    [Fact]
    public void OfflineSyncService_ProcessBatch_ConflictWhenItemCheckedOutByDifferentUser()
    {
        // Arrange: alice (id 2) already holds item 1
        var (sync, equipment, _) = CreateServices();
        equipment.Checkout(1, "alice", borrowerUserId: 2);

        // Bob (id 3) tries to checkout item 1
        var tx = MakeCheckout(itemId: 1, borrowerUserId: 3);

        // Act
        var results = sync.ProcessBatch(new[] { tx }, requestingUserId: 3);

        // Assert
        Assert.Single(results);
        Assert.Equal("conflict", results[0].Status);
        Assert.NotNull(results[0].ConflictDetails);
        // Item should still belong to alice
        Assert.Equal("alice", equipment.GetCurrentHolder(1));
    }

    [Fact]
    public void OfflineSyncService_ProcessBatch_FirstTransactionWinsOnConflict()
    {
        // Arrange: two offline checkouts for the same item, alice first, then bob
        var (sync, equipment, _) = CreateServices();
        var t1 = MakeCheckout(itemId: 1, borrowerUserId: 2, ts: DateTime.UtcNow.AddMinutes(-10)); // alice
        var t2 = MakeCheckout(itemId: 1, borrowerUserId: 3, ts: DateTime.UtcNow.AddMinutes(-5));  // bob (later)

        // Act — process as a batch (service must sort by OfflineTimestamp)
        // A coordinator (id 1) submits the consolidated batch from multiple devices
        var results = sync.ProcessBatch(new[] { t2, t1 }, requestingUserId: 1); // coordinator

        // Assert: alice's earlier transaction wins
        var aliceResult = results.First(r => r.DeviceTransactionId == t1.DeviceTransactionId);
        var bobResult   = results.First(r => r.DeviceTransactionId == t2.DeviceTransactionId);

        Assert.Equal("success",  aliceResult.Status);
        Assert.Equal("conflict", bobResult.Status);
        Assert.Equal("alice", equipment.GetCurrentHolder(1));
    }

    [Fact]
    public void OfflineSyncService_ProcessBatch_SecondTransactionConflict_CreatesCoordinatorNotification()
    {
        // Arrange: item 1 checked out by alice; bob's offline checkout conflicts
        var (sync, equipment, users) = CreateServices();
        equipment.Checkout(1, "alice", borrowerUserId: 2);

        var coordinator = users.GetById(1)!; // coord (id=1) is the coordinator
        var tx = MakeCheckout(itemId: 1, borrowerUserId: 3); // bob's offline transaction

        // We need to verify a notification was created — wire up a real notification service
        var notifications = new CoordinatorNotificationService();
        var sync2 = new OfflineSyncService(equipment, notifications, users);

        // Act
        sync2.ProcessBatch(new[] { tx }, requestingUserId: 3);

        // Assert: coordinator received a notification
        var pending = notifications.GetPendingForCoordinator(coordinator.Id);
        Assert.NotEmpty(pending);
        Assert.Contains("conflict", pending[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OfflineSyncService_ProcessBatch_DuplicateDeviceTransactionId_Idempotent()
    {
        // Arrange
        var (sync, equipment, _) = CreateServices();
        var id = Guid.NewGuid().ToString();
        var tx = MakeCheckout(itemId: 1, borrowerUserId: 2, id: id);

        // Act — submit same transaction twice
        var first  = sync.ProcessBatch(new[] { tx }, requestingUserId: 2);
        var second = sync.ProcessBatch(new[] { tx }, requestingUserId: 2);

        // Assert: both calls return "success" and item is checked out only once
        Assert.Equal("success", first[0].Status);
        Assert.Equal("success", second[0].Status);
        Assert.False(equipment.GetItem(1)!.IsAvailable);

        // Idempotency: only one checkout record should exist for item 1
        var history = equipment.GetCheckoutHistory(1);
        Assert.Single(history);
    }

    [Fact]
    public void OfflineSyncService_ProcessBatch_ChronologicalOrder_EarlierTimestampProcessedFirst()
    {
        // Arrange: item 1; checkout by alice at T-10min, return by alice at T-5min
        var (sync, equipment, _) = CreateServices();
        var early = MakeCheckout(itemId: 1, borrowerUserId: 2, ts: DateTime.UtcNow.AddMinutes(-10));
        var later = MakeReturn( itemId: 1, borrowerUserId: 2, ts: DateTime.UtcNow.AddMinutes(-5));

        // Act — submit in reverse order; service must sort chronologically
        var results = sync.ProcessBatch(new[] { later, early }, requestingUserId: 2);

        // Assert: both succeed; item ends up returned (available)
        Assert.All(results, r => Assert.Equal("success", r.Status));
        Assert.True(equipment.GetItem(1)!.IsAvailable);
    }

    [Fact]
    public void OfflineSyncService_ProcessBatch_BorrowerUserIdMismatch_ReturnsError()
    {
        // Arrange: alice (id 2) submits a transaction claiming to be for bob (id 3)
        var (sync, _, _) = CreateServices();
        var tx = MakeCheckout(itemId: 1, borrowerUserId: 3); // BorrowerUserId = bob

        // Act: requesting user is alice (2) — not a coordinator
        var results = sync.ProcessBatch(new[] { tx }, requestingUserId: 2);

        // Assert: error because non-coordinator cannot submit for another user
        Assert.Single(results);
        Assert.Equal("error", results[0].Status);
        Assert.Contains("does not match", results[0].ConflictDetails, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OfflineSyncService_GetResult_ReturnsResultForKnownId()
    {
        // Arrange
        var (sync, _, _) = CreateServices();
        var id = Guid.NewGuid().ToString();
        var tx = MakeCheckout(itemId: 1, borrowerUserId: 2, id: id);
        sync.ProcessBatch(new[] { tx }, requestingUserId: 2);

        // Act
        var result = sync.GetResult(id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(id, result!.DeviceTransactionId);
        Assert.Equal("success", result.Status);
    }

    [Fact]
    public void CheckoutRecord_OfflineTimestamp_SetFromOfflineTransaction()
    {
        // Arrange
        var (sync, equipment, _) = CreateServices();
        var offlineTs = new DateTime(2026, 3, 15, 10, 30, 0, DateTimeKind.Utc);
        var tx = MakeCheckout(itemId: 1, borrowerUserId: 2, ts: offlineTs);

        // Act
        sync.ProcessBatch(new[] { tx }, requestingUserId: 2);

        // Assert: the CheckoutRecord for item 1 has OfflineTimestamp set
        var record = equipment.GetActiveCheckoutRecord(1);
        Assert.NotNull(record);
        Assert.Equal(offlineTs, record!.OfflineTimestamp);
    }
}
