using System;
using System.Collections.Generic;
using System.Text;
using ECommerce.Business.DTOs.Auth;
using ECommerce.Business.Services;
using ECommerce.DataAccess.Repositories.Interfaces;
using ECommerce.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.Text.Json;

namespace ECommerce.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            PasswordHash = "hashed-password",
            Role = "Customer"
        };

        var userRepository =
            new Mock<IUserRepository>();

        userRepository
            .Setup(x => x.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(user);

        var passwordHasher =
            new Mock<IPasswordHasher<User>>();

        passwordHasher
            .Setup(x => x.VerifyHashedPassword(
                user,
                user.PasswordHash,
                "WrongPassword"))
            .Returns(PasswordVerificationResult.Failed);

        var configuration =
            new Mock<IConfiguration>();

        var service = new AuthService(
            userRepository.Object,
            passwordHasher.Object,
            configuration.Object);

        var dto = new LoginDto
        {
            Email = "test@example.com",
            Password = "WrongPassword"
        };

        // Act
        var result =
            await service.LoginAsync(dto);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RegisterAsync_NormalizesEmailHashesPasswordAndDoesNotGrantPrivilegedRole()
    {
        var users = new Mock<IUserRepository>();
        users
            .Setup(repository => repository.GetByEmailAsync("customer@example.com"))
            .ReturnsAsync((User?)null);
        users
            .Setup(repository => repository.AddAsync(It.IsAny<User>()))
            .ReturnsAsync((User user) =>
            {
                user.Id = 3;
                return user;
            });

        var passwordHasher = new Mock<IPasswordHasher<User>>();
        passwordHasher
            .Setup(hasher => hasher.HashPassword(It.IsAny<User>(), "secret123"))
            .Returns("hashed-password");

        var configuration = CreateJwtConfiguration();
        var service = new AuthService(users.Object, passwordHasher.Object, configuration.Object);

        var result = await service.RegisterAsync(new RegisterDto
        {
            FirstName = " Test ",
            LastName = " Customer ",
            Email = " CUSTOMER@example.com ",
            Password = "secret123",
            Role = "Vendor"
        });

        Assert.Equal(3, result.UserId);
        Assert.Equal("customer@example.com", result.Email);
        Assert.Equal("Customer", result.Role);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(
            result.Token,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "ECommerceAPI",
                ValidateAudience = true,
                ValidAudience = "ECommerceClient",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes("ThisIsAValidDevelopmentSigningKey123456789")),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            },
            out var validatedToken);
        Assert.Equal("Customer", principal.FindFirst(ClaimTypes.Role)?.Value);
        Assert.True(validatedToken.ValidTo > DateTime.UtcNow);
        users.Verify(repository => repository.AddAsync(It.Is<User>(user =>
            user.Email == "customer@example.com" &&
            user.FirstName == "Test" &&
            user.LastName == "Customer" &&
            user.PasswordHash == "hashed-password" &&
            user.Role == "Customer")), Times.Once);
        passwordHasher.Verify(
            hasher => hasher.HashPassword(It.IsAny<User>(), "secret123"),
            Times.Once);
    }

    [Theory]
    [InlineData("Vendor")]
    [InlineData("Admin")]
    public void RegisterDto_IgnoresRequestedPrivilegedRole(string role)
    {
        var dto = JsonSerializer.Deserialize<RegisterDto>(
            $$"""{"FirstName":"Test","LastName":"Customer","Email":"test@example.com","Password":"secret123","Role":"{{role}}"}""");

        Assert.NotNull(dto);
        Assert.Equal("Customer", dto.Role);
    }

    [Fact]
    public async Task RegisterAsync_RejectsExistingEmail()
    {
        var users = new Mock<IUserRepository>();
        users
            .Setup(repository => repository.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(new User { Email = "test@example.com" });

        var service = new AuthService(
            users.Object,
            new Mock<IPasswordHasher<User>>().Object,
            CreateJwtConfiguration().Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegisterAsync(new RegisterDto
            {
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                Password = "secret123"
            }));

        users.Verify(
            repository => repository.AddAsync(It.IsAny<User>()),
            Times.Never);
    }

    private static Mock<IConfiguration> CreateJwtConfiguration()
    {
        var configuration = new Mock<IConfiguration>();
        configuration.SetupGet(value => value["Jwt:Key"])
            .Returns("ThisIsAValidDevelopmentSigningKey123456789");
        configuration.SetupGet(value => value["Jwt:Issuer"])
            .Returns("ECommerceAPI");
        configuration.SetupGet(value => value["Jwt:Audience"])
            .Returns("ECommerceClient");
        configuration.SetupGet(value => value["Jwt:ExpiryMinutes"])
            .Returns("60");
        return configuration;
    }
}