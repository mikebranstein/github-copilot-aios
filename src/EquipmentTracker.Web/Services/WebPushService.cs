using EquipmentTracker.Web.Models;
using WebPush;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Sends VAPID-signed Web Push notifications.
/// IMPORTANT: Replace VapidPublicKey and VapidPrivateKey with real values in production.
/// HTTPS is required for Web Push in production; HTTP dev mode will not deliver push events to browsers.
/// </summary>
public class WebPushService : IPushNotificationService
{
    private readonly ILogger<WebPushService> _logger;
    private readonly IConfiguration _configuration;

    public WebPushService(ILogger<WebPushService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SendAsync(ApplicationUser user, string title, string body)
    {
        if (user.PushEndpoint is null || user.PushP256dh is null || user.PushAuth is null)
        {
            _logger.LogDebug("User {UserId} has no push subscription; skipping notification.", user.Id);
            return;
        }

        if (!user.NotificationsEnabled)
        {
            _logger.LogDebug("User {UserId} has notifications disabled; skipping.", user.Id);
            return;
        }

        var vapidPublicKey = _configuration["WebPush:VapidPublicKey"] ?? "PLACEHOLDER_REPLACE_IN_PRODUCTION";
        var vapidPrivateKey = _configuration["WebPush:VapidPrivateKey"] ?? "PLACEHOLDER_REPLACE_IN_PRODUCTION";
        var subject = _configuration["WebPush:Subject"] ?? "mailto:admin@example.com";

        try
        {
            var subscription = new PushSubscription(user.PushEndpoint, user.PushP256dh, user.PushAuth);
            var vapidDetails = new VapidDetails(subject, vapidPublicKey, vapidPrivateKey);
            var client = new WebPushClient();

            var payload = System.Text.Json.JsonSerializer.Serialize(new { title, body });
            await client.SendNotificationAsync(subscription, payload, vapidDetails);
            _logger.LogInformation("Push notification sent to user {UserId}.", user.Id);
        }
        catch (Exception ex)
        {
            // Unreachable endpoint or invalid subscription: log and skip; retry on next cycle.
            _logger.LogError(ex, "Failed to send push notification to user {UserId}. Will retry next cycle.", user.Id);
        }
    }
}
