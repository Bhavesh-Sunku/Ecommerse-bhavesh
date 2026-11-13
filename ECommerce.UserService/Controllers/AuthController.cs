using ECommerce.UserService.Contracts;
using ECommerce.UserService.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthenticationService authenticationService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        try
        {
            return Ok(await authenticationService.RegisterAsync(request));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Registration failed",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var response = await authenticationService.LoginAsync(request);
        return response is null
            ? Unauthorized(new ProblemDetails
            {
                Title = "Invalid email or password.",
                Status = StatusCodes.Status401Unauthorized
            })
            : Ok(response);
    }
}
