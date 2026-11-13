using ECommerce.UserService.Context;
using ECommerce.UserService.Contracts;
using ECommerce.UserService.Models;
using ECommerce.UserService.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace ECommerce.Tests;

public class UserServiceBoundaryTests
{
    [Fact]
    public async Task RegisterAndLoginAsync_HashesPasswordAndIssuesToken()
    {
        await using var context = CreateContext();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "local-test-signing-key-with-at-least-32-bytes",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();
        var service = new AuthenticationService(
            context,
            new PasswordHasher<UserAccount>(),
            configuration,
            NullLogger<AuthenticationService>.Instance);

        var registered = await service.RegisterAsync(new RegisterRequest
        {
            FirstName = "Demo",
            LastName = "Customer",
            Email = "demo@example.com",
            Password = "StrongPassword123!",
            Role = "Vendor"
        });
        var loggedIn = await service.LoginAsync(new LoginRequest
        {
            Email = "DEMO@example.com",
            Password = "StrongPassword123!"
        });
        var storedUser = await context.Users.SingleAsync();

        Assert.NotEmpty(registered.Token);
        Assert.Equal("Customer", registered.Role);
        Assert.Equal(registered.UserId, loggedIn?.UserId);
        Assert.NotEqual("StrongPassword123!", storedUser.PasswordHash);

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(
            registered.Token,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "test-issuer",
                ValidateAudience = true,
                ValidAudience = "test-audience",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes("local-test-signing-key-with-at-least-32-bytes")),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            },
            out var validatedToken);

        Assert.Equal("Customer", principal.FindFirst(ClaimTypes.Role)?.Value);
        Assert.True(validatedToken.ValidTo > DateTime.UtcNow);

        Assert.Throws<SecurityTokenInvalidAudienceException>(() =>
            tokenHandler.ValidateToken(
                registered.Token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "test-issuer",
                    ValidateAudience = true,
                    ValidAudience = "wrong-audience",
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes("local-test-signing-key-with-at-least-32-bytes")),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                },
                out _));

        Assert.ThrowsAny<SecurityTokenException>(() =>
            tokenHandler.ValidateToken(
                registered.Token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "test-issuer",
                    ValidateAudience = true,
                    ValidAudience = "test-audience",
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes("a-different-local-test-signing-key")),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                },
                out _));
    }

    [Fact]
    public async Task RegisterAsync_RejectsDuplicateEmail()
    {
        await using var context = CreateContext();
        var service = new AuthenticationService(
            context,
            new PasswordHasher<UserAccount>(),
            CreateConfiguration(),
            NullLogger<AuthenticationService>.Instance);
        var request = new RegisterRequest
        {
            FirstName = "Demo",
            LastName = "Customer",
            Email = "demo@example.com",
            Password = "StrongPassword123!"
        };

        await service.RegisterAsync(request);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegisterAsync(request));
    }

    private static UserDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new UserDbContext(options);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "local-test-signing-key-with-at-least-32-bytes",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();
    }
}
