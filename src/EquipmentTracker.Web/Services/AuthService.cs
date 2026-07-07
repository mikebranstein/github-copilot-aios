using EquipmentTracker.Web.Models;

namespace EquipmentTracker.Web.Services;

public class AuthService : IAuthService
{
    private readonly IUserService _userService;

    public AuthService(IUserService userService)
    {
        _userService = userService;
    }

    public ApplicationUser? Login(string username, string password)
    {
        var user = _userService.GetByUsername(username);
        if (user is null) return null;
        return _userService.ValidatePassword(user, password) ? user : null;
    }
}
