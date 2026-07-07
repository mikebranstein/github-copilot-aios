using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Background hosted service that runs daily and sends batched expiry alert digests.
/// Checks certifications expiring within 14, 30, and 60 days.
/// Sends at most one digest per recipient per day to prevent alert fatigue (AC6).
/// </summary>
public class CertExpiryNotificationJob : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    private readonly ICertificationService _certService;
    private readonly ILogger<CertExpiryNotificationJob> _logger;

    // Tracks the last notification date per recipient to prevent duplicate digests.
    private readonly Dictionary<string, DateTime> _lastNotifiedDate = new();

    public CertExpiryNotificationJob(
        ICertificationService certService,
        ILogger<CertExpiryNotificationJob> logger)
    {
        _certService = certService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CertExpiryNotificationJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                RunDailyDigest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CertExpiryNotificationJob encountered an error during daily digest run.");
            }

            await Task.Delay(RunInterval, stoppingToken);
        }

        _logger.LogInformation("CertExpiryNotificationJob stopped.");
    }

    /// <summary>
    /// Collects all certs expiring within 60 days (catches the 60/30/14-day thresholds),
    /// groups by operator/supervisor pair, and logs a digest for each unique recipient.
    /// </summary>
    internal void RunDailyDigest()
    {
        _certService.RefreshCertStatuses();

        // Collect certs at each threshold
        var expiring60 = _certService.GetCertsExpiringSoon(60);
        if (!expiring60.Any())
        {
            _logger.LogDebug("CertExpiryNotificationJob: No certs expiring within 60 days. No digest sent.");
            return;
        }

        // In MVP the EHS manager recipient is represented as a system log entry.
        // Production would look up the operator's supervisor and send an email digest.
        var today = DateTime.UtcNow.Date;
        var recipientKey = "EHSManager";

        if (_lastNotifiedDate.TryGetValue(recipientKey, out var lastDate) && lastDate.Date == today)
        {
            _logger.LogDebug("CertExpiryNotificationJob: Digest already sent today for {Recipient}. Skipping.", recipientKey);
            return;
        }

        _lastNotifiedDate[recipientKey] = DateTime.UtcNow;

        // Log the digest (production would send an email here)
        var certTypeLookup = _certService.GetAllCertTypes().ToDictionary(c => c.Id, c => c.Name);

        foreach (var cert in expiring60)
        {
            var certName = certTypeLookup.TryGetValue(cert.CertTypeId, out var n) ? n : $"cert #{cert.CertTypeId}";
            var daysUntil = (int)(cert.ExpiryDate - DateTime.UtcNow).TotalDays;
            var threshold = daysUntil <= 14 ? 14 : daysUntil <= 30 ? 30 : 60;

            _logger.LogInformation(
                "[CERT EXPIRY DIGEST] Operator={OperatorName} | Cert={CertName} | ExpiresOn={ExpiryDate:yyyy-MM-dd} | DaysRemaining={Days} | AlertThreshold={Threshold}",
                cert.OperatorName, certName, cert.ExpiryDate, daysUntil, threshold);
        }

        _logger.LogInformation(
            "CertExpiryNotificationJob: Daily digest sent. CertsExpiring={Count}", expiring60.Count);
    }
}
