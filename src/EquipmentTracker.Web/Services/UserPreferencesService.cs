using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

/// <summary>
/// In-memory user preferences store.
/// Added for Issue #118 — Real-Time Equipment Availability Dashboard.
/// </summary>
public class UserPreferencesService : IUserPreferencesService
{
    private readonly Dictionary<int, UserPreferences> _prefs = new();

    public UserPreferences? GetPreferences(int userId) =>
        _prefs.TryGetValue(userId, out var prefs) ? prefs : null;

    public void SavePreferences(int userId, string? siteFilter, string? categoryFilter)
    {
        _prefs[userId] = new UserPreferences
        {
            UserId = userId,
            PreferredSiteFilter = siteFilter,
            PreferredCategoryFilter = categoryFilter
        };
    }
}
