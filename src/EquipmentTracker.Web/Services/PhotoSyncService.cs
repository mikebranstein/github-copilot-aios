using System.Security.Cryptography;
using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-memory implementation of IPhotoSyncService.
/// Queues offline-captured photos and syncs them with retry logic.
/// Added for Issue #58 — Photo-Backed Checkout &amp; Return.
/// </summary>
public class PhotoSyncService : IPhotoSyncService
{
    private readonly List<OfflineSyncTransaction> _pending = new();
    private readonly HashSet<string> _uploadedKeys = new();

    // Allows tests to inject a delay factory (default: real Task.Delay)
    internal Func<int, Task>? DelayFactory { get; set; }

    private Task Delay(int ms) =>
        DelayFactory is not null ? DelayFactory(ms) : Task.Delay(ms);

    /// <inheritdoc />
    public Task QueueForSyncAsync(int checkoutRecordId, string photoLocalPath, bool isReturn = false)
    {
        var uploadKey = GenerateUploadKey(photoLocalPath);

        _pending.Add(new OfflineSyncTransaction
        {
            DeviceTransactionId = Guid.NewGuid().ToString("N"),
            Type = isReturn ? "return-photo" : "checkout-photo",
            ItemId = checkoutRecordId,
            BorrowerUserId = 0,
            OfflineTimestamp = DateTime.UtcNow,
            DeviceId = "server",
            PhotoLocalPath = photoLocalPath,
            PhotoUploadKey = uploadKey
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// Attempts each pending item up to 3 times with exponential back-off (1 s, 2 s, 4 s).
    /// In tests, DelayFactory can be set to a no-op to avoid real waits.
    public async Task<int> SyncPendingPhotosAsync()
    {
        int synced = 0;
        var toSync = _pending.Where(t => t.PhotoUploadKey is not null).ToList();

        foreach (var transaction in toSync)
        {
            // Skip duplicates — deduplication via SHA-256 key (AC-SYNC2)
            if (transaction.PhotoUploadKey is not null &&
                _uploadedKeys.Contains(transaction.PhotoUploadKey))
            {
                _pending.Remove(transaction);
                continue;
            }

            bool success = false;
            for (int attempt = 0; attempt < 3 && !success; attempt++)
            {
                try
                {
                    // Simulate upload attempt (always succeeds in the stub)
                    success = await TryUploadAsync(transaction);
                }
                catch
                {
                    // ignore and retry
                }

                if (!success && attempt < 2)
                {
                    int delayMs = (int)Math.Pow(2, attempt) * 1000; // 1s, 2s
                    await Delay(delayMs);
                }
            }

            if (success)
            {
                if (transaction.PhotoUploadKey is not null)
                    _uploadedKeys.Add(transaction.PhotoUploadKey);
                _pending.Remove(transaction);
                synced++;
            }
        }

        return synced;
    }

    /// <inheritdoc />
    public bool HasPendingSync => _pending.Count > 0;

    /// <inheritdoc />
    public bool PendingSyncIndicatorVisible => HasPendingSync;

    // ── Internals ────────────────────────────────────────────────────────────

    /// <summary>
    /// Overridable in tests to simulate upload failures.
    /// Default: always succeeds.
    /// </summary>
    internal Func<OfflineSyncTransaction, Task<bool>>? UploadHandler { get; set; }

    private Task<bool> TryUploadAsync(OfflineSyncTransaction transaction)
    {
        if (UploadHandler is not null)
            return UploadHandler(transaction);

        // Default stub: succeeds immediately
        return Task.FromResult(true);
    }

    private static string GenerateUploadKey(string localPath)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(localPath);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
