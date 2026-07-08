namespace EquipmentTracker.Web.Services;

/// <summary>
/// Background job that runs every minute to expire stale soft holds and
/// fire Notify Me alerts for items that became available after a hold expired.
/// Added for Issue #118 — Real-Time Equipment Availability Dashboard.
/// </summary>
public class SoftHoldExpiryJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly ISoftHoldService _softHoldService;
    private readonly INotifyMeService _notifyMeService;
    private readonly ILogger<SoftHoldExpiryJob> _logger;

    public SoftHoldExpiryJob(
        ISoftHoldService softHoldService,
        INotifyMeService notifyMeService,
        ILogger<SoftHoldExpiryJob> logger)
    {
        _softHoldService = softHoldService;
        _notifyMeService = notifyMeService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SoftHoldExpiryJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);

                var expiredItemIds = await _softHoldService.ExpireStaleHoldsAsync();

                foreach (var itemId in expiredItemIds)
                {
                    _logger.LogInformation("Soft hold expired for item {ItemId}; firing availability alerts.", itemId);
                    await _notifyMeService.FireAvailabilityAlertsAsync(itemId);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SoftHoldExpiryJob encountered an error. Will retry next cycle.");
            }
        }

        _logger.LogInformation("SoftHoldExpiryJob stopped.");
    }
}
