namespace EquipmentTracker.Web.Services;

/// <summary>
/// SMS sending interface, abstracting the external provider (e.g. Twilio).
/// The stub implementation logs without sending. Replace with a real Twilio-backed
/// implementation before the feature ships (per BA constraint: SMS fallback is non-negotiable).
/// Added for Issue #118 — Real-Time Equipment Availability Dashboard.
/// </summary>
public interface ISmsService
{
    /// <summary>
    /// Sends an SMS message to the given phone number.
    /// Returns true if the message was dispatched (does not guarantee delivery).
    /// </summary>
    Task<bool> SendAsync(string toPhoneNumber, string message);
}
