namespace User.Tests;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Controllers;
using User.Application.Interfaces;
using User.Application.Models;
using User.Application.Services.Auth;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services.Redis;
using Xunit;

public sealed class AuthControllerRestTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"user_api_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task Register_With_DisplayName_Returns_Ok_With_User_Info()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);
        var controller = new AuthController(authService, NullLogger<AuthController>.Instance);

        var request = new RegisterApiRequest(
            Email: "newrestuser@nexus.local",
            Password: "Password123!",
            DisplayName: "Nguyen Van B",
            PhoneNumber: "0912345678"
        );

        var result = await controller.Register(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RegisterApiResponse>(okResult.Value);
        Assert.Equal(response.Id, response.UserId);
        Assert.Equal(300, response.OtpExpiresIn);
    }

    [Fact]
    public async Task VerifyOtp_Returns_Ok_When_Otp_Matches()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);
        var controller = new AuthController(authService, NullLogger<AuthController>.Instance);

        await authService.RegisterAsync(new RegisterDto(
            Email: "verifytest@nexus.local",
            Password: "Password123!",
            FirstName: "Test",
            LastName: "User"
        ));

        var otpRecord = await otpService.GetOtpAsync("verifytest@nexus.local");
        Assert.NotNull(otpRecord);

        var verifyRequest = new VerifyOtpApiRequest(
            Email: "verifytest@nexus.local",
            Otp: otpRecord.Code
        );

        var result = await controller.VerifyOtp(verifyRequest, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<VerifyOtpApiResponse>(okResult.Value);
        Assert.True(response.IsVerified);
    }

    [Fact]
    public async Task ResendOtp_Returns_Ok()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);
        var controller = new AuthController(authService, NullLogger<AuthController>.Instance);

        await authService.RegisterAsync(new RegisterDto(
            Email: "resendtest@nexus.local",
            Password: "Password123!",
            FirstName: "Test",
            LastName: "User"
        ));

        var request = new ResendOtpApiRequest(Email: "resendtest@nexus.local");
        var result = await controller.ResendOtp(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ResendOtpApiResponse>(okResult.Value);
        Assert.Equal(60, response.ResendCooldown);
    }

    [Fact]
    public async Task Logout_Returns_NoContent()
    {
        using var dbContext = CreateInMemoryDbContext();
        var rawToken = "sample-valid-refresh-token";
        var tokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsRevoked = false
        });
        await dbContext.SaveChangesAsync();

        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);
        var controller = new AuthController(authService, NullLogger<AuthController>.Instance);

        var request = new LogoutApiRequest(RefreshToken: rawToken);
        var result = await controller.Logout(request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Login_With_Identifier_Returns_Ok_With_User_And_Roles()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var jwtGenerator = new TestJwtTokenGenerator();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);
        var controller = new AuthController(authService, NullLogger<AuthController>.Instance);

        var user = new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Username = "staff_member",
            Email = "staff.member@nexus.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            FirstName = "Staff",
            LastName = "Support",
            Status = UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var role = new Role
        {
            Id = Guid.NewGuid(),
            RoleCode = RoleType.SupportStaff,
            RoleName = "Support Staff",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleId = role.Id,
            Role = role,
            AssignedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        user.UserRoles = new List<UserRole> { userRole };

        dbContext.Users.Add(user);
        dbContext.Roles.Add(role);
        dbContext.UserRoles.Add(userRole);
        await dbContext.SaveChangesAsync();

        // Login using Identifier
        var loginRequest = new LoginApiRequest(
            Identifier: "staff_member",
            Password: "Password123!"
        );

        var result = await controller.Login(loginRequest, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginApiResponse>(okResult.Value);
        Assert.NotNull(response.User);
        Assert.Equal("staff_member", response.User.Username);
        Assert.Contains("staff", response.User.Roles);
        Assert.Contains("staff", response.Roles);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal(3600, response.ExpiresIn);
    }

    private sealed class TestJwtTokenGenerator : IJwtTokenGenerator
    {
        public string GenerateToken(Domain.Entities.User user, System.Collections.Generic.IEnumerable<string> roles, out DateTime expiresAt)
        {
            expiresAt = DateTime.UtcNow.AddMinutes(120);
            return "mock-jwt-access-token-xyz";
        }
    }

    private sealed class TestRedisOtpService : IRedisOtpService
    {
        private readonly ConcurrentDictionary<string, OtpEntry> _storage = new(StringComparer.OrdinalIgnoreCase);

        private static string Key(string email, string purpose) => $"{purpose}:{email}";

        public Task SaveOtpAsync(string email, string code, TimeSpan validityDuration, TimeSpan retentionDuration, string purpose = "registration")
        {
            _storage[Key(email, purpose)] = new OtpEntry
            {
                Code = code,
                Email = email,
                Status = OtpStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.Add(validityDuration)
            };
            return Task.CompletedTask;
        }

        public Task<OtpEntry?> GetOtpAsync(string email, string purpose = "registration")
        {
            _storage.TryGetValue(Key(email, purpose), out var entry);
            return Task.FromResult(entry);
        }

        public Task MarkOtpConsumedAsync(string email, string purpose = "registration")
        {
            if (_storage.TryGetValue(Key(email, purpose), out var entry))
            {
                entry.Status = OtpStatus.Consumed;
                entry.ConsumedAt = DateTimeOffset.UtcNow;
            }
            return Task.CompletedTask;
        }

        public Task InvalidateExistingOtpsAsync(string email, string purpose = "registration")
        {
            if (_storage.TryGetValue(Key(email, purpose), out var entry) && entry.Status == OtpStatus.Active)
            {
                entry.Status = OtpStatus.Expired;
            }
            return Task.CompletedTask;
        }
    }
}
