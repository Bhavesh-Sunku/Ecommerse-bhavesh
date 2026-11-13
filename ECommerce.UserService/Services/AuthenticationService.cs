using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ECommerce.UserService.Context;
using ECommerce.UserService.Contracts;
using ECommerce.UserService.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ECommerce.UserService.Services;

public class AuthenticationService(
    UserDbContext context,
    IPasswordHasher<UserAccount> passwordHasher,
    IConfiguration configuration,
    ILogger<AuthenticationService> logger)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await context.Users.AnyAsync(user => user.Email == email))
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        var user = new UserAccount
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            Role = "Customer",
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        context.Users.Add(user);
        await context.SaveChangesAsync();
        logger.LogInformation("Registered user {UserId}", user.Id);
        return CreateResponse(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await context.Users.FirstOrDefaultAsync(item => item.Email == email);
        if (user is null ||
            passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password) == PasswordVerificationResult.Failed)
        {
            return null;
        }

        logger.LogInformation("Authenticated user {UserId}", user.Id);
        return CreateResponse(user);
    }

    public async Task<UserProfile?> GetProfileAsync(int userId)
    {
        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId);

        return user is null ? null : new UserProfile
        {
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };
    }

    private AuthResponse CreateResponse(UserAccount user)
    {
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT key is not configured.");
        var issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("JWT issuer is not configured.");
        var audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("JWT audience is not configured.");
        var expirySetting = configuration["Jwt:ExpiryMinutes"];
        var expiryMinutes = expirySetting is null
            ? 60
            : int.TryParse(expirySetting, out var configuredExpiry)
                ? configuredExpiry
                : 0;
        if (expiryMinutes <= 0)
        {
            throw new InvalidOperationException(
                "JWT expiry must be configured as a positive number of minutes.");
        }
        if (Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException(
                "JWT signing key must contain at least 32 UTF-8 bytes.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new AuthResponse
        {
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role,
            Token = new JwtSecurityTokenHandler().WriteToken(token)
        };
    }
}
