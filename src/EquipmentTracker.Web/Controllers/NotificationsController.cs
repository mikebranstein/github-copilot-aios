using EquipmentTracker.Web.Services;
using EquipmentTracker.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EquipmentTracker.Web.Controllers;

[Authorize]
[Route("api/notifications")]
[ApiController]
public class NotificationsController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IConfiguration _configuration;

    public NotificationsController(IUserService userService, IConfiguration configuration)
    {
        _userService = userService;
        _configuration = configuration;
    }

    [HttpGet("vapid-public-key")]
    public IActionResult GetVapidPublicKey()
    {
        var key = _configuration["WebPush:VapidPublicKey"] ?? "PLACEHOLDER_REPLACE_IN_PRODUCTION";
        return Ok(new { publicKey = key });
    }

    [HttpPost("subscribe")]
    public IActionResult Subscribe([FromBody] PushSubscriptionViewModel model)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        _userService.UpdatePushSubscription(userId, model.Endpoint, model.P256dh, model.Auth);
        return Ok();
    }
}
