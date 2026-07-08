namespace EquipmentTracker.Web.Services;

/// <summary>
/// Stub SMS service that logs outbound messages without sending them.
/// IMPORTANT: Replace this with a real Twilio (or equivalent) implementation before shipping.
/// Per BA constraint: push-only notification is insufficient — SMS fallback is non-negotiable.
/// Added for Issue #118 — Real-Time Equipment Availability Dashboard.
/// </summary>
public class StubSmsService : ISmsService
{
    private readonly ILogger<StubSmsService> _logger;

    public StubSmsService(ILogger<StubSmsService> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(string toPhoneNumber, string message)
    {
        _logger.LogWarning(
            "[SMS STUB] Would send to {PhoneNumber}: {Message}. " +
            "Replace StubSmsService with a real Twilio integration before shipping.",
            toPhoneNumber, message);
        return Task.FromResult(true);
    }
}
