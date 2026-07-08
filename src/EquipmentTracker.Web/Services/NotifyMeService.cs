using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-memory implementation of the Notify Me subscription and alert system.
/// Push notification is attempted first; SMS fallback is sent when push cannot be confirmed
/// (no push subscription registered or push failed), per BA constraint.
/// Added for Issue #118 — Real-Time Equipment Availability Dashboard.
/// </summary>
public class NotifyMeService : INotifyMeService
{
    private readonly List<NotifyMeSubscription> _subscriptions = new();
    private readonly IEquipmentService _equipmentService;
    private readonly ISoftHoldService _softHoldService;
    private readonly IAvailabilityDashboardService _dashboardService;
    private readonly IUserService _userService;
    private readonly IPushNotificationService _pushService;
    private readonly ISmsService _smsService;
    private readonly ILogger<NotifyMeService> _logger;
    private int _nextId = 1;

    public NotifyMeService(
        IEquipmentService equipmentService,
        ISoftHoldService softHoldService,
        IAvailabilityDashboardService dashboardService,
        IUserService userService,
        IPushNotificationService pushService,
        ISmsService smsService,
        ILogger<NotifyMeService> logger)
    {
        _equipmentService = equipmentService;
        _softHoldService = softHoldService;
        _dashboardService = dashboardService;
        _userService = userService;
        _pushService = pushService;
        _smsService = smsService;
        _logger = logger;
    }

    public Task<NotifyMeSubscription> SubscribeToItemAsync(int userId, int equipmentItemId)
    {
        // Idempotent: return existing active subscription if one exists
        var existing = _subscriptions.FirstOrDefault(s =>
            s.UserId == userId &&
            s.EquipmentItemId == equipmentItemId &&
            s.IsActive);

        if (existing is not null)
            return Task.FromResult(existing);

        var sub = new NotifyMeSubscription
        {
            Id = _nextId++,
            UserId = userId,
            EquipmentItemId = equipmentItemId,
            CreatedAtUtc = DateTime.UtcNow
        };
        _subscriptions.Add(sub);
        return Task.FromResult(sub);
    }

    public Task<NotifyMeSubscription> SubscribeToCategoryAsync(int userId, string category)
    {
        var existing = _subscriptions.FirstOrDefault(s =>
            s.UserId == userId &&
            s.Category == category &&
            s.EquipmentItemId is null &&
            s.IsActive);

        if (existing is not null)
            return Task.FromResult(existing);

        var sub = new NotifyMeSubscription
        {
            Id = _nextId++,
            UserId = userId,
            Category = category,
            CreatedAtUtc = DateTime.UtcNow
        };
        _subscriptions.Add(sub);
        return Task.FromResult(sub);
    }

    public Task<bool> CancelSubscriptionAsync(int subscriptionId, int userId)
    {
        var sub = _subscriptions.FirstOrDefault(s => s.Id == subscriptionId && s.UserId == userId);
        if (sub is null || !sub.IsActive)
            return Task.FromResult(false);

        sub.CancelledAtUtc = DateTime.UtcNow;
        return Task.FromResult(true);
    }

    public IReadOnlyList<NotifyMeSubscription> GetActiveSubscriptionsForUser(int userId) =>
        _subscriptions.Where(s => s.UserId == userId && s.IsActive).ToList().AsReadOnly();

    public async Task FireAvailabilityAlertsAsync(int equipmentItemId)
    {
        var item = _equipmentService.GetItem(equipmentItemId);
        if (item is null) return;

        // Only fire when item is compound-available
        var availability = _dashboardService.GetItemAvailability(equipmentItemId);
        if (availability?.CompoundStatus != EquipmentCompoundStatus.Available) return;

        // Find all matching active subscriptions
        var matching = _subscriptions.Where(s =>
            s.IsActive &&
            (s.EquipmentItemId == equipmentItemId ||
             (s.EquipmentItemId is null && s.Category == item.Category)));

        foreach (var sub in matching.ToList())
        {
            var user = _userService.GetById(sub.UserId);
            if (user is null) continue;

            var title = $"Equipment Available: {item.Name}";
            var body = $"{item.Name} ({item.Category}) at {item.SiteName ?? "unknown site"} is now available.";

            // Attempt push notification first
            bool pushDelivered = false;
            if (user.PushEndpoint is not null && user.NotificationsEnabled)
            {
                try
                {
                    await _pushService.SendAsync(user, title, body);
                    pushDelivered = true;
                    _logger.LogInformation("Notify Me push sent to user {UserId} for item {ItemId}", user.Id, equipmentItemId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Push notification failed for user {UserId}; falling back to SMS", user.Id);
                }
            }

            // SMS fallback: send if push was not delivered
            if (!pushDelivered && !string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                await _smsService.SendAsync(user.PhoneNumber, $"{title} — {body}");
                _logger.LogInformation("Notify Me SMS fallback sent to user {UserId} for item {ItemId}", user.Id, equipmentItemId);
            }

            sub.LastFiredAtUtc = DateTime.UtcNow;
            // Cancel the subscription after firing so user doesn't get repeat alerts
            sub.CancelledAtUtc = DateTime.UtcNow;
        }
    }
}
