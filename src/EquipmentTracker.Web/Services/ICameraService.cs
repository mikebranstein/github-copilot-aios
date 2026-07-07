namespace EquipmentTracker.Web.Services;

/// <summary>
/// Abstracts camera access for photo capture during checkout/return.
/// Added for Issue #58 — Photo-Backed Checkout &amp; Return.
/// </summary>
public interface ICameraService
{
    /// <summary>
    /// Opens the camera with no blocking network calls — satisfies AC-C6.
    /// </summary>
    Task<bool> OpenAsync();

    /// <summary>Returns photo bytes (mock-friendly).</summary>
    Task<byte[]?> CapturePhotoAsync();
}
