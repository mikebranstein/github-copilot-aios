using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Background job that checks for overdue equipment every 5 minutes and sends push notifications.
/// Uses an in-memory SentNotificationLog — notifications are not persisted across restarts.
/// </summary>
public class OverdueNotificationJob : BackgroundService
{
    private readonly ILogger<OverdueNotificationJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    // In-memory log: tracks which (checkoutRecordId, notificationType) pairs have already been notified.
    // Reset on application restart.
    private readonly List<SentNotification> _sentLog = new();

    public OverdueNotificationJob(
        ILogger<OverdueNotificationJob> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _configuration.GetValue<int>("Notifications:PollingIntervalMinutes", 5);
        if (intervalMinutes <= 0) intervalMinutes = 5;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunNotificationCycleAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in overdue notification job cycle.");
            }
        }
    }

    internal async Task RunNotificationCycleAsync()
    {
        var equipmentService = _serviceProvider.GetRequiredService<IEquipmentService>();
        var userService = _serviceProvider.GetRequiredService<IUserService>();
        var pushService = _serviceProvider.GetRequiredService<IPushNotificationService>();
        var approvalService = _serviceProvider.GetRequiredService<IApprovalService>();

        // Auto-approve pending approvals that have expired their timeout
        var timeoutMinutes = _configuration.GetValue<int>("Approval:AutoApproveTimeoutMinutes", 5);
        if (timeoutMinutes <= 0) timeoutMinutes = 5;
        approvalService.AutoApproveExpired(TimeSpan.FromMinutes(timeoutMinutes));

        var overdueThresholdDays = _configuration.GetValue<int>("Checkout:OverdueThresholdDays", 7);
        if (overdueThresholdDays <= 0) overdueThresholdDays = 7;

        var coordinatorExtraHours = _configuration.GetValue<int>("Notifications:CoordinatorOverdueHours", 24);

        var allItems = equipmentService.GetAllItems();

        foreach (var item in allItems.Where(i => !i.IsAvailable))
        {
            var record = equipmentService.GetActiveCheckoutRecord(item.Id);
            if (record is null) continue;

            var age = DateTime.UtcNow - record.CheckedOutAtUtc;

            if (age.TotalDays >= overdueThresholdDays && record.BorrowerUserId.HasValue)
            {
                var alreadySent = _sentLog.Any(s =>
                    s.CheckoutRecordId == record.Id &&
                    s.NotificationType == NotificationType.BorrowerOverdue);

                if (!alreadySent)
                {
                    var borrower = userService.GetById(record.BorrowerUserId.Value);
                    if (borrower is not null && borrower.NotificationsEnabled)
                    {
                        await pushService.SendAsync(
                            borrower,
                            "Equipment Overdue",
                            $"Your equipment '{item.Name}' is overdue. Please return it as soon as possible.");

                        _sentLog.Add(new SentNotification
                        {
                            CheckoutRecordId = record.Id,
                            NotificationType = NotificationType.BorrowerOverdue,
                            SentAtUtc = DateTime.UtcNow
                        });
                    }
                }
            }

            if (age.TotalDays >= overdueThresholdDays + (coordinatorExtraHours / 24.0))
            {
                var alreadySentCoord = _sentLog.Any(s =>
                    s.CheckoutRecordId == record.Id &&
                    s.NotificationType == NotificationType.CoordinatorOverdue);

                if (!alreadySentCoord)
                {
                    var coordinators = userService.GetCoordinators();
                    foreach (var coordinator in coordinators.Where(c => c.NotificationsEnabled))
                    {
                        await pushService.SendAsync(
                            coordinator,
                            "Equipment Severely Overdue",
                            $"Equipment '{item.Name}' (checked out by {record.BorrowerName}) is severely overdue.");
                    }

                    _sentLog.Add(new SentNotification
                    {
                        CheckoutRecordId = record.Id,
                        NotificationType = NotificationType.CoordinatorOverdue,
                        SentAtUtc = DateTime.UtcNow
                    });
                }
            }
        }
    }

    /// <summary>Exposed for testing: returns a copy of the sent notification log.</summary>
    public IReadOnlyList<SentNotification> GetSentLog() => _sentLog.AsReadOnly();
}
