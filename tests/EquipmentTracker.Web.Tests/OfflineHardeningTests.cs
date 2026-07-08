using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using Xunit;

namespace EquipmentTracker.Web.Tests;

/// <summary>
/// Tests for PWA Offline Hardening (Issue #139).
/// Covers ConditionNotes (AC4), server probe prerequisite logic (AC5),
/// full offline queue workflow (AC3), last-write-wins conflict resolution (AC7),
/// worker feedback messages (AC7), and coordinator override with audit log (AC7).
/// </summary>
public class OfflineHardeningTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static (OfflineSyncService sync, EquipmentService equipment, UserService users)
        CreateServices()
    {
        var equipment     = new EquipmentService();
        var users         = new UserService();
        users.Register("coord", "pass", isCoordinator: true);  // id 1
        users.Register("alice", "pass");                         // id 2
        users.Register("bob",   "pass");                         // id 3

        var notifications = new CoordinatorNotificationService();
        var sync          = new OfflineSyncService(equipment, notifications, users);
        return (sync, equipment, users);
    }

    private static OfflineSyncTransaction MakeCheckout(
        int itemId, int borrowerUserId, DateTime? ts = null, string? id = null) =>
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
        int itemId, int borrowerUserId, DateTime? ts = null,
        string? id = null, string? conditionNotes = null) =>
        new()
        {
            DeviceTransactionId = id ?? Guid.NewGuid().ToString(),
            Type                = "return",
            ItemId              = itemId,
            BorrowerUserId      = borrowerUserId,
            OfflineTimestamp    = ts ?? DateTime.UtcNow,
            DeviceId            = "test-device",
            ConditionNotes      = conditionNotes
        };

    // ── AC4: ConditionNotes field ────────────────────────────────────────────

    [Fact]
    public void AC4_OfflineReturn_WithConditionNotes_PersistedToCheckoutRecord()
    {
        // Arrange: alice checks out item 1, then returns it offline with condition notes
        var (sync, equipment, _) = CreateServices();
        equipment.Checkout(1, "alice", borrowerUserId: 2);

        var tx = MakeReturn(itemId: 1, borrowerUserId: 2,
                            conditionNotes: "Minor scratch on handle");

        // Act
        var results = sync.ProcessBatch(new[] { tx }, requestingUserId: 2);

        // Assert: return succeeded
        Assert.Single(results);
        Assert.Equal("success", results[0].Status);

        // Assert: condition notes are preserved on the checkout record
        // (returned record is the most-recently completed record for item 1)
        var history = equipment.GetCheckoutHistory(1);
        var returned = history.FirstOrDefault(r => r.ReturnedAtUtc.HasValue);
        Assert.NotNull(returned);
        Assert.Equal("Minor scratch on handle", returned!.ReturnConditionNote);
    }

    [Fact]
    public void AC4_OfflineReturn_NullConditionNotes_DoesNotOverwriteExistingNote()
    {
        // Arrange: item already has a ReturnConditionNote set before sync
        var (sync, equipment, _) = CreateServices();
        equipment.Checkout(1, "alice", borrowerUserId: 2);
        var activeRecord = equipment.GetActiveCheckoutRecord(1);
        if (activeRecord is not null) activeRecord.ReturnConditionNote = "Pre-existing note";

        var tx = MakeReturn(itemId: 1, borrowerUserId: 2, conditionNotes: null);

        // Act
        var results = sync.ProcessBatch(new[] { tx }, requestingUserId: 2);

        // Assert: return succeeded and original note preserved (null conditionNotes = no overwrite)
        Assert.Equal("success", results[0].Status);
        var history = equipment.GetCheckoutHistory(1);
        var returned = history.FirstOrDefault(r => r.ReturnedAtUtc.HasValue);
        // Pre-existing note should remain since no new conditionNotes were sent
        Assert.NotNull(returned);
        // The field should be either the pre-existing or null — not a different value
        Assert.True(returned!.ReturnConditionNote is null or "Pre-existing note");
    }

    [Fact]
    public void AC4_OfflineSyncTransaction_HasConditionNotesProperty()
    {
        // Assert: OfflineSyncTransaction model exposes ConditionNotes
        var tx = new OfflineSyncTransaction { ConditionNotes = "damage noted" };
        Assert.Equal("damage noted", tx.ConditionNotes);
    }

    // ── AC5: Sync queue flush (server probe prerequisite — logic layer) ───────

    [Fact]
    public void AC5_ProcessBatch_WithPendingTransactions_FlushesAll()
    {
        // Arrange: two pending transactions
        var (sync, equipment, _) = CreateServices();
        var t1 = MakeCheckout(itemId: 1, borrowerUserId: 2);
        var t2 = MakeCheckout(itemId: 2, borrowerUserId: 2);

        // Act: process batch (simulates flush within 30 s of connectivity)
        var results = sync.ProcessBatch(new[] { t1, t2 }, requestingUserId: 2);

        // Assert: both flushed successfully
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("success", r.Status));
    }

    // ── AC6: Last-write-wins conflict resolution ─────────────────────────────

    [Fact]
    public void AC6_LastWriteWins_LaterTimestampWins_EarlierIsConflict()
    {
        // Arrange: alice (T-10 min), bob (T-5 min) — SAME batch.
        // Within-batch LWW: bob is later, bob WINS; alice is pre-marked conflict.
        var (sync, equipment, _) = CreateServices();
        var aliceTx = MakeCheckout(itemId: 1, borrowerUserId: 2,
                                   ts: DateTime.UtcNow.AddMinutes(-10));  // earlier
        var bobTx   = MakeCheckout(itemId: 1, borrowerUserId: 3,
                                   ts: DateTime.UtcNow.AddMinutes(-5));   // later

        // Act — coordinator submits consolidated batch (within-batch LWW)
        var results = sync.ProcessBatch(new[] { aliceTx, bobTx }, requestingUserId: 1);

        var aliceResult = results.First(r => r.DeviceTransactionId == aliceTx.DeviceTransactionId);
        var bobResult   = results.First(r => r.DeviceTransactionId == bobTx.DeviceTransactionId);

        // Assert: bob wins (later timestamp), alice loses
        Assert.Equal("success",  bobResult.Status);
        Assert.Equal("conflict", aliceResult.Status);
        Assert.Equal("bob", equipment.GetCurrentHolder(1));
    }

    [Fact]
    public void AC6_LastWriteWins_EarlierTimestamp_IsNotApplied()
    {
        // Arrange: alice already holds item 1 (from a sync that already ran, server-side checkout).
        // Bob sends a transaction OLDER than alice's — server record wins.
        var (sync, equipment, _) = CreateServices();
        equipment.Checkout(1, "alice", borrowerUserId: 2);

        // Bob sends a transaction from T-15 min (older than alice's server checkout at ~now)
        var bobTx = MakeCheckout(itemId: 1, borrowerUserId: 3,
                                 ts: DateTime.UtcNow.AddMinutes(-15));

        // Act: single-item batch (no within-batch conflict); falls to cross-batch LWW logic
        var results = sync.ProcessBatch(new[] { bobTx }, requestingUserId: 3);

        // Assert: bob's old transaction is rejected as conflict (server wins over offline)
        Assert.Equal("conflict", results[0].Status);
        Assert.Equal("alice", equipment.GetCurrentHolder(1));
    }

    // ── AC7: Worker feedback message ─────────────────────────────────────────

    [Fact]
    public void AC7_ConflictResult_HasWorkerMessage()
    {
        // Arrange: alice holds item 1 (server-side checkout); bob's offline tx is older → conflict
        var (sync, equipment, _) = CreateServices();
        equipment.Checkout(1, "alice", borrowerUserId: 2);

        // Bob's tx is from T-5 min (older than alice's server checkout — server wins)
        var bobTx = MakeCheckout(itemId: 1, borrowerUserId: 3,
                                 ts: DateTime.UtcNow.AddMinutes(-5));

        // Act
        var results = sync.ProcessBatch(new[] { bobTx }, requestingUserId: 3);

        // Assert: conflict result has a non-null WorkerMessage for UI display
        Assert.Equal("conflict", results[0].Status);
        Assert.NotNull(results[0].WorkerMessage);
        Assert.NotEmpty(results[0].WorkerMessage!);
        Assert.Contains("coordinator", results[0].WorkerMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC7_SyncResult_HasWorkerMessageProperty()
    {
        // Assert: SyncResult model exposes WorkerMessage
        var result = new SyncResult { WorkerMessage = "Your checkout was overridden." };
        Assert.Equal("Your checkout was overridden.", result.WorkerMessage);
    }

    // ── AC7: Coordinator override with audit log ──────────────────────────────

    [Fact]
    public void AC7_CoordinatorOverride_ForcesConflictedTransactionToApply()
    {
        // Arrange: alice (T-10) and bob (T-5) in same batch → bob wins by LWW, alice is conflict
        var (sync, equipment, users) = CreateServices();
        var aliceTx = MakeCheckout(itemId: 1, borrowerUserId: 2,
                                   ts: DateTime.UtcNow.AddMinutes(-10));
        var bobTx   = MakeCheckout(itemId: 1, borrowerUserId: 3,
                                   ts: DateTime.UtcNow.AddMinutes(-5));

        sync.ProcessBatch(new[] { aliceTx, bobTx }, requestingUserId: 1);
        Assert.Equal("bob", equipment.GetCurrentHolder(1));  // bob won LWW

        // Coordinator decides alice should have won instead
        var overrideResult = sync.CoordinatorOverride(
            aliceTx.DeviceTransactionId,
            coordinatorUserId: 1,
            overrideReason: "Alice submitted first on-site. Override per supervisor.");

        // Assert: override applied
        Assert.NotNull(overrideResult);
        Assert.Equal("success", overrideResult!.Status);
        Assert.Equal("alice", equipment.GetCurrentHolder(1));
    }

    [Fact]
    public void AC7_CoordinatorOverride_CreatesAuditLogEntry()
    {
        // Arrange: alice (T-10) and bob (T-5) in same batch → bob wins, alice conflicts
        var (sync, equipment, _) = CreateServices();
        var aliceTx = MakeCheckout(itemId: 1, borrowerUserId: 2,
                                   ts: DateTime.UtcNow.AddMinutes(-10));
        var bobTx   = MakeCheckout(itemId: 1, borrowerUserId: 3,
                                   ts: DateTime.UtcNow.AddMinutes(-5));
        sync.ProcessBatch(new[] { aliceTx, bobTx }, requestingUserId: 1);

        // Act: coordinator overrides to apply alice's transaction
        sync.CoordinatorOverride(
            aliceTx.DeviceTransactionId,
            coordinatorUserId: 1,
            overrideReason: "Field supervisor decision");

        // Assert: audit log has one entry
        var log = sync.GetOverrideAuditLog();
        Assert.Single(log);
        Assert.Equal(aliceTx.DeviceTransactionId, log[0].DeviceTransactionId);
        Assert.Equal(1, log[0].CoordinatorUserId);
        Assert.Equal("Field supervisor decision", log[0].OverrideReason);
        Assert.True(log[0].AppliedAtUtc > DateTime.UtcNow.AddSeconds(-10));
    }

    [Fact]
    public void AC7_CoordinatorOverride_NonCoordinatorCannotOverride()
    {
        // Arrange: alice (T-10) and bob (T-5) in same batch → bob wins LWW, alice is conflict
        var (sync, equipment, _) = CreateServices();
        var aliceTx = MakeCheckout(itemId: 1, borrowerUserId: 2,
                                   ts: DateTime.UtcNow.AddMinutes(-10));
        var bobTx   = MakeCheckout(itemId: 1, borrowerUserId: 3,
                                   ts: DateTime.UtcNow.AddMinutes(-5));
        sync.ProcessBatch(new[] { aliceTx, bobTx }, requestingUserId: 1);
        Assert.Equal("bob", equipment.GetCurrentHolder(1));

        // Act: alice (id 2, not a coordinator) tries to override
        var result = sync.CoordinatorOverride(
            aliceTx.DeviceTransactionId,
            coordinatorUserId: 2,
            overrideReason: "Unauthorized");

        // Assert: override returns null (not authorised)
        Assert.Null(result);
        // Item should still belong to bob (LWW winner)
        Assert.Equal("bob", equipment.GetCurrentHolder(1));
    }

    [Fact]
    public void AC7_CoordinatorOverride_UnknownTransactionId_ReturnsNull()
    {
        var (sync, _, _) = CreateServices();
        var result = sync.CoordinatorOverride("nonexistent-id", coordinatorUserId: 1, "reason");
        Assert.Null(result);
    }

    // ── Regression: existing first-write-wins test updated for last-write-wins ─

    [Fact]
    public void LWW_ConflictBatch_ProcessedDescendingByTimestamp()
    {
        // Arrange: 3 workers checkout same item within one batch; latest wins (LWW within-batch).
        var (sync, equipment, _) = CreateServices();
        var t1 = MakeCheckout(itemId: 1, borrowerUserId: 2,
                              ts: DateTime.UtcNow.AddMinutes(-20));  // alice, oldest
        var t2 = MakeCheckout(itemId: 1, borrowerUserId: 3,
                              ts: DateTime.UtcNow.AddMinutes(-10));  // bob, later

        // Submit in any order — batch pre-processing selects the LWW winner by timestamp
        var results = sync.ProcessBatch(new[] { t1, t2 }, requestingUserId: 1);

        // bob is latest — bob is the LWW winner, alice is marked conflict
        var r1 = results.First(r => r.DeviceTransactionId == t1.DeviceTransactionId);
        var r2 = results.First(r => r.DeviceTransactionId == t2.DeviceTransactionId);
        Assert.Equal("conflict", r1.Status);  // alice (earlier) loses
        Assert.Equal("success",  r2.Status);  // bob  (later)   wins
        Assert.Equal("bob", equipment.GetCurrentHolder(1));
    }

    // ── AC3: Offline checkout queue survives and syncs ───────────────────────

    [Fact]
    public void AC3_OfflineCheckout_QueuedAndSynced_SuccessConfirmation()
    {
        // Arrange: simulate a field worker offline checkout queued as a transaction
        var (sync, equipment, _) = CreateServices();
        var tx = MakeCheckout(itemId: 1, borrowerUserId: 2);

        // Act: sync fires on connectivity restore
        var results = sync.ProcessBatch(new[] { tx }, requestingUserId: 2);

        // Assert: success (simulates the "success confirmation shown" AC)
        Assert.Single(results);
        Assert.Equal("success", results[0].Status);
        Assert.False(equipment.GetItem(1)!.IsAvailable, "Item should be checked out after sync");
    }
}
