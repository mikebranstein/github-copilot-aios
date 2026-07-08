namespace EquipmentTracker.Web.Models;

/// <summary>
/// Upload state for a single condition photo.
/// Added for Issue #115 — Equipment Condition Assessment &amp; Damage Tracking at Return.
/// </summary>
public enum PhotoUploadStatus
{
    Complete,
    Pending,
    Failed
}

/// <summary>
/// Photo evidence attached to a ConditionRecord.
/// Added for Issue #115 — Equipment Condition Assessment &amp; Damage Tracking at Return.
/// </summary>
public class ConditionPhoto
{
    public int Id { get; set; }

    /// <summary>FK to parent ConditionRecord.</summary>
    public int ConditionRecordId { get; set; }

    /// <summary>Server-side URL or local path to the stored photo file.</summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>Server-side timestamp applied at upload time (NOT device time). Tamper-evident.</summary>
    public DateTime ServerTimestampUtc { get; set; }

    /// <summary>Wall-clock time when the offline sync was queued (null if uploaded online).</summary>
    public DateTime? SyncQueuedAt { get; set; }

    /// <summary>Wall-clock time when the offline sync completed (null if pending).</summary>
    public DateTime? SyncCompletedAt { get; set; }

    /// <summary>Current upload/sync state.</summary>
    public PhotoUploadStatus UploadStatus { get; set; } = PhotoUploadStatus.Complete;

    /// <summary>Number of upload retry attempts made (for exponential-backoff retry logic).</summary>
    public int RetryCount { get; set; } = 0;
}
