namespace EquipmentTracker.Web.Models;

/// <summary>
/// Result for a single offline transaction after server-side sync processing.
/// </summary>
public class SyncResult
{
    public string DeviceTransactionId { get; set; } = string.Empty;

    /// <summary>"success" | "conflict" | "error"</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Present only when Status == "conflict" or "error".</summary>
    public string? ConflictDetails { get; set; }
}
