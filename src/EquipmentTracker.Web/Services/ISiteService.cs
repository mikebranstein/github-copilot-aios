using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public interface ISiteService
{
    IReadOnlyList<Site> GetAllSites();
    IReadOnlyList<Site> GetActiveSites();
    Site? GetSite(int id);
    Site? CreateSite(string name);
    bool RenameSite(int id, string newName);
    bool DeactivateSite(int id);
    bool ActivateSite(int id);
}
