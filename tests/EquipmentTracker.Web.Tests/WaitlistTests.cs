using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EquipmentTracker.Web.Tests;

public class FakePushNotificationService : IPushNotificationService
{
    public List<(ApplicationUser User, string Title, string Body)> Sent { get; } = new();

    public Task SendAsync(ApplicationUser user, string title, string body)
    {
        Sent.Add((user, title, body));
        return Task.CompletedTask;
    }
}

public class FakeUserService : IUserService
{
    private readonly ApplicationUser _defaultUser = new() { Id = 1, Username = "testuser", NotificationsEnabled = true };

    public ApplicationUser? Register(string username, string password, bool isCoordinator = false) => null;
    public ApplicationUser? GetByUsername(string username) => _defaultUser;
    public ApplicationUser? GetById(int id) => new ApplicationUser { Id = id, Username = $"user{id}", NotificationsEnabled = true };
    public bool ValidatePassword(ApplicationUser user, string password) => true;
    public IReadOnlyList<ApplicationUser> GetCoordinators() => new List<ApplicationUser>();
    public IReadOnlyList<ApplicationUser> GetBorrowers() => new List<ApplicationUser> { _defaultUser };
    public void UpdatePushSubscription(int userId, string? endpoint, string? p256dh, string? auth) { }
    public void SetNotificationsEnabled(int userId, bool enabled) { }
    // Added for Issue #117
    public IReadOnlyList<ApplicationUser> GetSafetyAdmins() => new List<ApplicationUser>();
    public IReadOnlyList<ApplicationUser> GetApprovers() => new List<ApplicationUser>();
    public void SetSafetyAdmin(int userId, bool isSafetyAdmin) { }
}

public class WaitlistTests
{
    private static (WaitlistService Service, EquipmentService EquipmentSvc, FakePushNotificationService PushSvc) CreateServices()
    {
        var equipmentSvc = new EquipmentService();
        var pushSvc = new FakePushNotificationService();
        var userSvc = new FakeUserService();
        var logger = NullLogger<WaitlistService>.Instance;
        var svc = new WaitlistService(equipmentSvc, pushSvc, userSvc, logger);
        return (svc, equipmentSvc, pushSvc);
    }

    [Fact]
    public async Task WE1_JoinQueueAsync_AddsWaitingEntryToQueue()
    {
        var (service, equipmentSvc, _) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();

        var entry = await service.JoinQueueAsync(item.Id, 11, "Alice");
        var queue = await service.GetQueueForItemAsync(item.Id);

        Assert.Equal(WaitlistStatus.Waiting, entry.Status);
        Assert.Contains(queue, e => e.Id == entry.Id && e.Status == WaitlistStatus.Waiting);
    }

    [Fact]
    public async Task WE2_UrgentEntriesComeFirst_ThenFifoWithinTier()
    {
        var (service, equipmentSvc, _) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();

        var standardOne = await service.JoinQueueAsync(item.Id, 1, "Standard A");
        var standardTwo = await service.JoinQueueAsync(item.Id, 2, "Standard B");
        var urgent = await service.JoinQueueAsync(item.Id, 3, "Urgent A", WaitlistTier.Urgent);

        var queue = await service.GetQueueForItemAsync(item.Id);

        Assert.Equal(urgent.Id, queue[0].Id);
        Assert.Equal(standardOne.Id, queue[1].Id);
        Assert.Equal(standardTwo.Id, queue[2].Id);
    }

    [Fact]
    public async Task WE3a_OverridePositionAsync_ReordersQueueAndPositions()
    {
        var (service, equipmentSvc, _) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();
        var first = await service.JoinQueueAsync(item.Id, 1, "A");
        var second = await service.JoinQueueAsync(item.Id, 2, "B");
        var third = await service.JoinQueueAsync(item.Id, 3, "C");

        var updated = await service.OverridePositionAsync(third.Id, 1, "Priority need", "Coordinator");
        var queue = await service.GetQueueForItemAsync(item.Id);

        Assert.True(updated);
        Assert.Equal(new[] { third.Id, first.Id, second.Id }, queue.Select(q => q.Id).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, queue.Select(q => q.QueuePosition).ToArray());
    }

    [Fact]
    public async Task WE3b_RemoveEntryAsync_RemovesEntryAndRecalculatesPositions()
    {
        var (service, equipmentSvc, _) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();
        var first = await service.JoinQueueAsync(item.Id, 1, "A");
        var second = await service.JoinQueueAsync(item.Id, 2, "B");
        var third = await service.JoinQueueAsync(item.Id, 3, "C");

        var removed = await service.RemoveEntryAsync(second.Id, "Duplicate request", "Coordinator");
        var queue = await service.GetQueueForItemAsync(item.Id);

        Assert.True(removed);
        Assert.Equal(new[] { first.Id, third.Id }, queue.Select(q => q.Id).ToArray());
        Assert.Equal(new[] { 1, 2 }, queue.Select(q => q.QueuePosition).ToArray());
    }

    [Fact]
    public async Task WE3c_MarkUrgentAsync_UpdatesTierAndRecalculatesPositions()
    {
        var (service, equipmentSvc, _) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();
        var first = await service.JoinQueueAsync(item.Id, 1, "A");
        var second = await service.JoinQueueAsync(item.Id, 2, "B");

        var updated = await service.MarkUrgentAsync(second.Id, "Coordinator");
        var queue = await service.GetQueueForItemAsync(item.Id);

        Assert.True(updated);
        Assert.Equal(second.Id, queue[0].Id);
        Assert.Equal(WaitlistTier.Urgent, queue[0].Tier);
    }

    [Fact]
    public async Task WE4_AdvanceQueueAndExpiryCascade_ReserveNextEligibleEntries()
    {
        var (service, equipmentSvc, _) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();
        var first = await service.JoinQueueAsync(item.Id, 1, "A");
        var second = await service.JoinQueueAsync(item.Id, 2, "B");

        await service.AdvanceQueueAsync(item.Id);
        var reserved = await service.GetEntryAsync(first.Id);
        Assert.Equal(WaitlistStatus.Reserved, reserved!.Status);

        reserved.ConfirmationDeadlineUtc = DateTime.UtcNow.AddMinutes(-1);
        SetEntryState(service, reserved);

        await service.ExpireTimedOutReservationsAsync();

        var forfeited = await service.GetEntryAsync(first.Id);
        var advanced = await service.GetEntryAsync(second.Id);
        Assert.Equal(WaitlistStatus.Forfeited, forfeited!.Status);
        Assert.Equal(WaitlistStatus.Reserved, advanced!.Status);
    }

    [Fact]
    public async Task WE5_SequentialJoins_KeepStableOrder()
    {
        var (service, equipmentSvc, _) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();

        var first = await service.JoinQueueAsync(item.Id, 1, "A");
        var second = await service.JoinQueueAsync(item.Id, 2, "B");
        var queue = await service.GetQueueForItemAsync(item.Id);

        Assert.Equal(first.Id, queue[0].Id);
        Assert.Equal(second.Id, queue[1].Id);
    }

    [Fact]
    public async Task QP1_GetPositionAndEtaAsync_ReturnsOneBasedPosition()
    {
        var (service, equipmentSvc, _) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();
        await service.JoinQueueAsync(item.Id, 1, "A");
        var second = await service.JoinQueueAsync(item.Id, 2, "B");

        var (position, _) = await service.GetPositionAndEtaAsync(second.Id);

        Assert.Equal(2, position);
    }

    [Fact]
    public async Task QP2a_GetPositionAndEtaAsync_ReturnsNotEnoughHistoryMessage()
    {
        var (service, equipmentSvc, _) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();
        var entry = await service.JoinQueueAsync(item.Id, 1, "A");

        var (_, eta) = await service.GetPositionAndEtaAsync(entry.Id);

        Assert.Equal("ETA unavailable — not enough history", eta);
    }

    [Fact]
    public async Task QP2b_GetPositionAndEtaAsync_ReturnsRangeWithEnoughHistory()
    {
        var (service, equipmentSvc, _) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();

        for (var i = 0; i < 3; i++)
        {
            var historyEntry = await service.JoinQueueAsync(item.Id, 100 + i, $"History {i}");
            await service.AdvanceQueueAsync(item.Id);
            await service.ConfirmReservationAsync(historyEntry.Id, 100 + i);
        }

        var active = await service.JoinQueueAsync(item.Id, 1, "A");
        var (_, eta) = await service.GetPositionAndEtaAsync(active.Id);

        Assert.StartsWith("~", eta);
        Assert.Contains("–", eta);
    }

    [Fact]
    public async Task QP3_PositionsUpdateAfterCancel()
    {
        var (service, equipmentSvc, _) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();
        var first = await service.JoinQueueAsync(item.Id, 1, "A");
        var second = await service.JoinQueueAsync(item.Id, 2, "B");
        var third = await service.JoinQueueAsync(item.Id, 3, "C");

        await service.CancelEntryAsync(second.Id, 2);
        var queue = await service.GetQueueForItemAsync(item.Id);

        Assert.Equal(new[] { first.Id, third.Id }, queue.Select(q => q.Id).ToArray());
        Assert.Equal(new[] { 1, 2 }, queue.Select(q => q.QueuePosition).ToArray());
    }

    [Fact]
    public async Task QP4a_WorkerCanCancelOwnEntry()
    {
        var (service, equipmentSvc, _) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();
        var entry = await service.JoinQueueAsync(item.Id, 1, "A");

        var cancelled = await service.CancelEntryAsync(entry.Id, 1);
        var updated = await service.GetEntryAsync(entry.Id);

        Assert.True(cancelled);
        Assert.Equal(WaitlistStatus.Cancelled, updated!.Status);
    }

    [Fact]
    public async Task QP4b_WorkerCannotCancelAnotherUsersEntry()
    {
        var (service, equipmentSvc, _) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();
        var entry = await service.JoinQueueAsync(item.Id, 1, "A");

        var cancelled = await service.CancelEntryAsync(entry.Id, 999);

        Assert.False(cancelled);
    }

    [Fact]
    public async Task AN1_AdvanceQueueAsync_SendsPushNotification()
    {
        var (service, equipmentSvc, pushSvc) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();
        await service.JoinQueueAsync(item.Id, 1, "A");

        await service.AdvanceQueueAsync(item.Id);

        Assert.Single(pushSvc.Sent);
    }

    [Fact]
    public async Task AN2_OverridePosition_SendsPushButJoinDoesNot()
    {
        var (service, equipmentSvc, pushSvc) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();
        var first = await service.JoinQueueAsync(item.Id, 1, "A");
        var second = await service.JoinQueueAsync(item.Id, 2, "B");

        Assert.Empty(pushSvc.Sent);

        await service.OverridePositionAsync(second.Id, 1, "Emergency", "Coordinator");

        Assert.Single(pushSvc.Sent);
    }

    [Fact]
    public async Task AN3_AdvanceQueueNotification_IncludesEquipmentName()
    {
        var (service, equipmentSvc, pushSvc) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();
        await service.JoinQueueAsync(item.Id, 1, "A");

        await service.AdvanceQueueAsync(item.Id);

        Assert.Contains(item.Name, pushSvc.Sent[0].Title + " " + pushSvc.Sent[0].Body);
    }

    [Fact]
    public async Task CQD1_GetAllActiveQueuesAsync_ReturnsItemsWithWaitingEntries()
    {
        var (service, equipmentSvc, _) = CreateServices();
        var firstItem = equipmentSvc.GetAllItems()[0];
        var secondItem = equipmentSvc.GetAllItems()[1];
        await service.JoinQueueAsync(firstItem.Id, 1, "A");
        await service.JoinQueueAsync(secondItem.Id, 2, "B");

        var queues = await service.GetAllActiveQueuesAsync();

        Assert.Contains(queues, q => q.EquipmentItemId == firstItem.Id);
        Assert.Contains(queues, q => q.EquipmentItemId == secondItem.Id);
    }

    [Fact]
    public async Task CQD2_CoordinatorActions_UpdatePositionsImmediately()
    {
        var (service, equipmentSvc, _) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();
        var first = await service.JoinQueueAsync(item.Id, 1, "A");
        var second = await service.JoinQueueAsync(item.Id, 2, "B");
        var third = await service.JoinQueueAsync(item.Id, 3, "C");

        await service.OverridePositionAsync(third.Id, 1, "Urgent project", "Coordinator");
        await service.MarkUrgentAsync(second.Id, "Coordinator");
        var queueAfterUrgent = await service.GetQueueForItemAsync(item.Id);
        Assert.Equal(second.Id, queueAfterUrgent[0].Id);

        await service.RemoveEntryAsync(second.Id, "Resolved", "Coordinator");
        var finalQueue = await service.GetQueueForItemAsync(item.Id);

        Assert.Equal(new[] { third.Id, first.Id }, finalQueue.Select(q => q.Id).ToArray());
        Assert.Equal(new[] { 1, 2 }, finalQueue.Select(q => q.QueuePosition).ToArray());
    }

    [Fact]
    public async Task CQD3_GetHistoryForItemAsync_ReturnsExitedEntries()
    {
        var (service, equipmentSvc, _) = CreateServices();
        var item = equipmentSvc.GetAllItems().First();
        var cancelled = await service.JoinQueueAsync(item.Id, 1, "A");
        var fulfilled = await service.JoinQueueAsync(item.Id, 2, "B");

        await service.CancelEntryAsync(cancelled.Id, 1);
        await service.AdvanceQueueAsync(item.Id);
        await service.ConfirmReservationAsync(fulfilled.Id, 2);

        var history = await service.GetHistoryForItemAsync(item.Id);

        Assert.Contains(history, h => h.Id == cancelled.Id && h.Status == WaitlistStatus.Cancelled);
        Assert.Contains(history, h => h.Id == fulfilled.Id && h.Status == WaitlistStatus.Fulfilled);
    }

    private static void SetEntryState(WaitlistService service, WaitlistEntry updatedEntry)
    {
        var entriesField = typeof(WaitlistService).GetField("_entries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(entriesField);
        var entries = entriesField!.GetValue(service) as List<WaitlistEntry>;
        Assert.NotNull(entries);
        var existing = entries!.Single(e => e.Id == updatedEntry.Id);
        existing.Status = updatedEntry.Status;
        existing.QueuePosition = updatedEntry.QueuePosition;
        existing.Tier = updatedEntry.Tier;
        existing.JoinedAtUtc = updatedEntry.JoinedAtUtc;
        existing.ReservedAtUtc = updatedEntry.ReservedAtUtc;
        existing.ConfirmationDeadlineUtc = updatedEntry.ConfirmationDeadlineUtc;
        existing.ExitedAtUtc = updatedEntry.ExitedAtUtc;
        existing.ExitReason = updatedEntry.ExitReason;
        existing.OverrideReason = updatedEntry.OverrideReason;
    }
}
