using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public interface IAuthService
{
    ApplicationUser? Login(string username, string password);
}
