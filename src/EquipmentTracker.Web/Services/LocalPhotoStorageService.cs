using System.Security.Cryptography;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-memory implementation of IPhotoStorageService.
/// Consistent with the codebase's no-IO, in-memory pattern.
/// Added for Issue #58 — Photo-Backed Checkout &amp; Return.
/// </summary>
public class LocalPhotoStorageService : IPhotoStorageService
{
    private const int MaxPhotoBytes = 2_097_152; // 2 MB

    private readonly Dictionary<int, string> _checkoutPhotos = new();
    private readonly Dictionary<int, string> _returnPhotos = new();
    private readonly IEquipmentService _equipmentService;

    public LocalPhotoStorageService(IEquipmentService equipmentService)
    {
        _equipmentService = equipmentService;
    }

    /// <inheritdoc />
    public Task<string> SavePhotoAsync(byte[] photoBytes, string uploaderName)
    {
        var compressed = CompressIfNeeded(photoBytes);
        var guid = Guid.NewGuid().ToString("N");
        var url = $"/photos/{guid}.jpg";
        return Task.FromResult(url);
    }

    /// <inheritdoc />
    public string? GetCheckoutPhotoUrl(int checkoutRecordId)
    {
        _checkoutPhotos.TryGetValue(checkoutRecordId, out var url);
        return url;
    }

    /// <inheritdoc />
    /// If bytes exceed 2 MB, truncate to 2 MB (mock compression for in-memory implementation).
    public byte[] CompressIfNeeded(byte[] photoBytes)
    {
        if (photoBytes.Length <= MaxPhotoBytes)
            return photoBytes;

        var result = new byte[MaxPhotoBytes];
        Array.Copy(photoBytes, result, MaxPhotoBytes);
        return result;
    }

    /// <inheritdoc />
    /// Returns the lowercase hex-encoded SHA-256 hash of the bytes.
    public string GenerateUploadKey(byte[] photoBytes)
    {
        var hash = SHA256.HashData(photoBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <inheritdoc />
    public Task AttachToCheckoutRecordAsync(int checkoutRecordId, string photoUrl, bool isReturn = false)
    {
        var record = _equipmentService.GetCheckoutRecordById(checkoutRecordId);
        if (record is not null)
        {
            if (isReturn)
            {
                record.ConditionPhotoAtReturn = photoUrl;
                _returnPhotos[checkoutRecordId] = photoUrl;
            }
            else
            {
                record.ConditionPhotoAtCheckout = photoUrl;
                _checkoutPhotos[checkoutRecordId] = photoUrl;
            }
        }
        return Task.CompletedTask;
    }
}
