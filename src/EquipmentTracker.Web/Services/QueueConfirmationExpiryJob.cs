namespace EquipmentTracker.Web.Services;

public class QueueConfirmationExpiryJob : BackgroundService
{
    private readonly ILogger<QueueConfirmationExpiryJob> _logger;
    private readonly IServiceProvider _serviceProvider;

    public QueueConfirmationExpiryJob(
        ILogger<QueueConfirmationExpiryJob> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var waitlistService = _serviceProvider.GetRequiredService<IWaitlistService>();
                await waitlistService.ExpireTimedOutReservationsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in queue confirmation expiry job.");
            }
        }
    }
}
