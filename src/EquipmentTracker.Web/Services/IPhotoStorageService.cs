namespace EquipmentTracker.Web.Services;

/// <summary>
/// Handles saving and retrieving condition photos for equipment checkout/return.
/// Added for Issue #58 — Photo-Backed Checkout &amp; Return.
/// </summary>
public interface IPhotoStorageService
{
    /// <summary>Save photo bytes, compress to ≤2MB, return URL path.</summary>
    Task<string> SavePhotoAsync(byte[] photoBytes, string uploaderName);

    /// <summary>Get photo URL for a checkout record.</summary>
    string? GetCheckoutPhotoUrl(int checkoutRecordId);

    /// <summary>Compress photo to ≤2MB (2,097,152 bytes).</summary>
    byte[] CompressIfNeeded(byte[] photoBytes);

    /// <summary>Generate deduplication key (SHA-256 hash of bytes).</summary>
    string GenerateUploadKey(byte[] photoBytes);

    /// <summary>Attach photo URL to an existing checkout record.</summary>
    Task AttachToCheckoutRecordAsync(int checkoutRecordId, string photoUrl, bool isReturn = false);
}
