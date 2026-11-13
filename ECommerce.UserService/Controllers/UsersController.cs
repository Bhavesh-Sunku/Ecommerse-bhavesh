using System.Security.Claims;
using ECommerce.UserService.Contracts;
using ECommerce.UserService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(AuthenticationService authenticationService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<UserProfile>> GetCurrentUser()
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idValue, out var userId))
        {
            return Unauthorized();
        }

        var profile = await authenticationService.GetProfileAsync(userId);
        return profile is null ? NotFound() : Ok(profile);
    }
}
