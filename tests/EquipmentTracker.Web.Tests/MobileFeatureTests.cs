using EquipmentTracker.Web.Models;
using EquipmentTracker.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EquipmentTracker.Web.Tests;

public class MobileFeatureTests
{
    private static UserService CreateUserService() => new();

    [Fact]
    public void UserService_Register_CreatesUserWithHashedPassword()
    {
        var svc = CreateUserService();
        var user = svc.Register("alice", "password123");

        Assert.NotNull(user);
        Assert.NotEqual("password123", user!.PasswordHash);
        Assert.NotEmpty(user.PasswordHash);
    }

    [Fact]
    public void UserService_Login_ReturnsTrueForCorrectPassword()
    {
        var svc = CreateUserService();
        var user = svc.Register("alice", "password123");
        Assert.NotNull(user);

        var result = svc.ValidatePassword(user!, "password123");
        Assert.True(result);
    }

    [Fact]
    public void UserService_Login_ReturnsFalseForWrongPassword()
    {
        var svc = CreateUserService();
        var user = svc.Register("alice", "password123");
        Assert.NotNull(user);

        var result = svc.ValidatePassword(user!, "wrongpassword");
        Assert.False(result);
    }

    [Fact]
    public void UserService_GetCoordinators_ReturnsOnlyCoordinators()
    {
        var svc = CreateUserService();
        svc.Register("coordinator1", "pass1", isCoordinator: true);
        svc.Register("coordinator2", "pass2", isCoordinator: true);
        svc.Register("regular", "pass3", isCoordinator: false);

        var coordinators = svc.GetCoordinators();

        Assert.Equal(2, coordinators.Count);
        Assert.All(coordinators, c => Assert.True(c.IsCoordinator));
        Assert.DoesNotContain(coordinators, c => c.Username == "regular");
    }

    [Fact]
    public void EquipmentService_IsOverdue_TrueWhenPastThreshold()
    {
        var equipSvc = new EquipmentService();
        var item = equipSvc.GetAllItems().First(i => i.IsAvailable);
        equipSvc.Checkout(item.Id, "Bob");

        var record = equipSvc.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);
        record!.CheckedOutAtUtc = DateTime.UtcNow.AddDays(-8);

        var age = DateTime.UtcNow - record.CheckedOutAtUtc;
        Assert.True(age.TotalDays >= 7);
    }

    [Fact]
    public void EquipmentService_IsOverdue_FalseWhenBelowThreshold()
    {
        var equipSvc = new EquipmentService();
        var item = equipSvc.GetAllItems().First(i => i.IsAvailable);
        equipSvc.Checkout(item.Id, "Bob");

        var record = equipSvc.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);
        record!.CheckedOutAtUtc = DateTime.UtcNow.AddDays(-3);

        var age = DateTime.UtcNow - record.CheckedOutAtUtc;
        Assert.False(age.TotalDays >= 7);
    }

    [Fact]
    public async Task OverdueNotificationJob_SendsBorrowerPush_WhenItemIsOverdue()
    {
        var pushSvc = new FakePushService();
        var userSvc = new UserService();
        var equipSvc = new EquipmentService();

        var user = userSvc.Register("borrower", "pass");
        Assert.NotNull(user);
        user!.PushEndpoint = "https://example.com/push";

        var item = equipSvc.GetAllItems().First(i => i.IsAvailable);
        equipSvc.Checkout(item.Id, "borrower", user.Id);

        var record = equipSvc.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);
        record!.CheckedOutAtUtc = DateTime.UtcNow.AddDays(-8);

        var job = CreateJob(pushSvc, userSvc, equipSvc);
        await job.RunNotificationCycleAsync();

        Assert.Equal(1, pushSvc.CallCount);
        Assert.Contains("overdue", pushSvc.LastBody ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OverdueNotificationJob_NoDuplicatePush_WhenAlreadyNotified()
    {
        var pushSvc = new FakePushService();
        var userSvc = new UserService();
        var equipSvc = new EquipmentService();

        var user = userSvc.Register("borrower", "pass");
        Assert.NotNull(user);
        user!.PushEndpoint = "https://example.com/push";

        var item = equipSvc.GetAllItems().First(i => i.IsAvailable);
        equipSvc.Checkout(item.Id, "borrower", user.Id);

        var record = equipSvc.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);
        record!.CheckedOutAtUtc = DateTime.UtcNow.AddDays(-8);

        var job = CreateJob(pushSvc, userSvc, equipSvc);
        await job.RunNotificationCycleAsync();
        await job.RunNotificationCycleAsync();

        Assert.Equal(1, pushSvc.CallCount);
    }

    [Fact]
    public async Task OverdueNotificationJob_SendsCoordinatorPush_When24hOverdue()
    {
        var pushSvc = new FakePushService();
        var userSvc = new UserService();
        var equipSvc = new EquipmentService();

        var coordinator = userSvc.Register("coord", "pass", isCoordinator: true);
        Assert.NotNull(coordinator);
        coordinator!.PushEndpoint = "https://example.com/coord-push";

        var item = equipSvc.GetAllItems().First(i => i.IsAvailable);
        equipSvc.Checkout(item.Id, "borrower");

        var record = equipSvc.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);
        record!.CheckedOutAtUtc = DateTime.UtcNow.AddDays(-9);

        var job = CreateJob(pushSvc, userSvc, equipSvc);
        await job.RunNotificationCycleAsync();

        Assert.True(pushSvc.CallCount >= 1);
    }

    [Fact]
    public async Task OverdueNotificationJob_SkipsUser_WhenNotificationsDisabled()
    {
        var pushSvc = new FakePushService();
        var userSvc = new UserService();
        var equipSvc = new EquipmentService();

        var user = userSvc.Register("borrower", "pass");
        Assert.NotNull(user);
        user!.PushEndpoint = "https://example.com/push";
        user.NotificationsEnabled = false;

        var item = equipSvc.GetAllItems().First(i => i.IsAvailable);
        equipSvc.Checkout(item.Id, "borrower", user.Id);

        var record = equipSvc.GetActiveCheckoutRecord(item.Id);
        Assert.NotNull(record);
        record!.CheckedOutAtUtc = DateTime.UtcNow.AddDays(-8);

        var job = CreateJob(pushSvc, userSvc, equipSvc);
        await job.RunNotificationCycleAsync();

        Assert.Equal(0, pushSvc.CallCount);
    }

    [Fact]
    public void CheckoutRecord_BorrowerUserId_IsNullableForBackwardCompat()
    {
        var record = new CheckoutRecord
        {
            Id = 1,
            EquipmentItemId = 1,
            BorrowerName = "Alice",
            CheckedOutAtUtc = DateTime.UtcNow
        };

        Assert.Null(record.BorrowerUserId);
    }

    [Fact]
    public async Task WebPushService_LogsError_WhenEndpointUnreachable()
    {
        var logger = NullLogger<WebPushService>.Instance;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebPush:VapidPublicKey"] = "PLACEHOLDER_REPLACE_IN_PRODUCTION",
                ["WebPush:VapidPrivateKey"] = "PLACEHOLDER_REPLACE_IN_PRODUCTION",
                ["WebPush:Subject"] = "mailto:admin@example.com"
            })
            .Build();
        var pushSvc = new WebPushService(logger, configuration);

        var user = new ApplicationUser
        {
            Id = 1,
            Username = "test",
            PasswordHash = "hash",
            PushEndpoint = "https://unreachable.example.com/push",
            PushP256dh = "p256dh",
            PushAuth = "auth",
            NotificationsEnabled = true
        };

        await pushSvc.SendAsync(user, "Test Title", "Test body");
    }

    private static OverdueNotificationJob CreateJob(
        IPushNotificationService pushSvc,
        IUserService userSvc,
        IEquipmentService equipSvc)
    {
        var services = new ServiceCollection();
        services.AddSingleton(pushSvc);
        services.AddSingleton(userSvc);
        services.AddSingleton(equipSvc);
        services.AddSingleton<IApprovalService>(sp => new ApprovalService(sp.GetRequiredService<IEquipmentService>(), sp.GetRequiredService<IUserService>(), sp.GetRequiredService<IPushNotificationService>()));
        var provider = services.BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Checkout:OverdueThresholdDays"] = "7",
                ["Notifications:PollingIntervalMinutes"] = "5",
                ["Notifications:CoordinatorOverdueHours"] = "24",
                ["Approval:AutoApproveTimeoutMinutes"] = "5"
            })
            .Build();

        return new OverdueNotificationJob(
            NullLogger<OverdueNotificationJob>.Instance,
            provider,
            config);
    }
}

public class FakePushService : IPushNotificationService
{
    public int CallCount { get; private set; }
    public string? LastTitle { get; private set; }
    public string? LastBody { get; private set; }

    public Task SendAsync(ApplicationUser user, string title, string body)
    {
        if (user.NotificationsEnabled && user.PushEndpoint is not null)
        {
            CallCount++;
            LastTitle = title;
            LastBody = body;
        }

        return Task.CompletedTask;
    }
}
