using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public class SiteService : ISiteService
{
    private readonly List<Site> _sites = new();
    private int _nextId = 1;
    private const int MaxSites = 50;

    public IReadOnlyList<Site> GetAllSites() => _sites.AsReadOnly();

    public IReadOnlyList<Site> GetActiveSites() => _sites.Where(s => s.IsActive).ToList().AsReadOnly();

    public Site? GetSite(int id) => _sites.FirstOrDefault(s => s.Id == id);

    public Site? CreateSite(string name)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName) || _sites.Count >= MaxSites)
            return null;

        var site = new Site
        {
            Id = _nextId++,
            Name = trimmedName,
            IsActive = true
        };

        _sites.Add(site);
        return site;
    }

    public bool RenameSite(int id, string newName)
    {
        var site = GetSite(id);
        var trimmedName = newName?.Trim() ?? string.Empty;
        if (site is null || string.IsNullOrWhiteSpace(trimmedName))
            return false;

        site.Name = trimmedName;
        return true;
    }

    public bool DeactivateSite(int id)
    {
        var site = GetSite(id);
        if (site is null)
            return false;

        site.IsActive = false;
        return true;
    }

    public bool ActivateSite(int id)
    {
        var site = GetSite(id);
        if (site is null)
            return false;

        site.IsActive = true;
        return true;
    }
}
