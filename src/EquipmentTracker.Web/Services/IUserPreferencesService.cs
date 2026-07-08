using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// Persists per-user dashboard filter preferences across sessions.
/// Added for Issue #118 — Real-Time Equipment Availability Dashboard.
/// </summary>
public interface IUserPreferencesService
{
    /// <summary>Returns the stored preferences for the given user, or null if none exist yet.</summary>
    UserPreferences? GetPreferences(int userId);

    /// <summary>Saves (upserts) preferences for the given user.</summary>
    void SavePreferences(int userId, string? siteFilter, string? categoryFilter);
}
