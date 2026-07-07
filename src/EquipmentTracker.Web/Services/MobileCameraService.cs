namespace EquipmentTracker.Web.Services;

/// <summary>
/// Server-side stub implementation of ICameraService.
/// Actual camera access is handled by browser JS — this stub keeps unit tests fast.
/// Added for Issue #58 — Photo-Backed Checkout &amp; Return.
/// </summary>
public class MobileCameraService : ICameraService
{
    /// <inheritdoc />
    /// No network calls — AC-C6 satisfied.
    public Task<bool> OpenAsync() => Task.FromResult(true);

    /// <inheritdoc />
    /// Returns null on the server side; real capture happens via browser JS.
    public Task<byte[]?> CapturePhotoAsync() => Task.FromResult<byte[]?>(null);
}
