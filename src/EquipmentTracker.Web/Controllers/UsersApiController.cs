using EquipmentTracker.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentTracker.Web.Controllers;

/// <summary>
/// API endpoints for user data.
/// GET /api/users/borrowers?search={term} — returns non-coordinator users as JSON.
/// </summary>
[Authorize]
[Route("api/users")]
[ApiController]
public class UsersApiController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersApiController(IUserService userService)
    {
        _userService = userService;
    }

    // GET /api/users/borrowers?search={term}
    [HttpGet("borrowers")]
    public IActionResult GetBorrowers([FromQuery] string? search)
    {
        var borrowers = _userService.GetBorrowers();

        if (!string.IsNullOrWhiteSpace(search))
        {
            borrowers = borrowers
                .Where(u => u.Username.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList()
                .AsReadOnly();
        }

        var result = borrowers.Select(u => new
        {
            id = u.Id,
            username = u.Username,
            isCoordinator = u.IsCoordinator
        });

        return Ok(result);
    }
}
