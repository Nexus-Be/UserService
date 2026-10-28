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
    public async Task Register_Returns_Created_With_UserId_And_OtpExpiresIn()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance);
        var controller = new AuthController(authService, NullLogger<AuthController>.Instance);

        var request = new RegisterApiRequest(
            Email: "newrestuser@nexus.local",
            Password: "Password123!",
            FirstName: "Nguyen",
            LastName: "Van B",
            PhoneNumber: "0912345678",
            Username: "nguyenvanb"
        );

        var result = await controller.Register(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
        var resp = Assert.IsType<RegisterApiResponse>(objectResult.Value);
        Assert.Equal("newrestuser@nexus.local", resp.Email);
        Assert.Equal(300, resp.OtpExpiresIn);
        Assert.False(string.IsNullOrWhiteSpace(resp.UserId));
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
        Assert.NotNull(okResult.Value);
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
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task Login_Returns_Ok_With_User_And_Tokens()
    {
        using var dbContext = CreateInMemoryDbContext();
        var otpService = new TestRedisOtpService();
        var jwtGenerator = new TestJwtTokenGenerator();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);
        var controller = new AuthController(authService, NullLogger<AuthController>.Instance);

        var registerDto = new RegisterDto("login_rest@nexus.local", "Password123!", "Nguyen", "Van C");
        var user = await authService.RegisterAsync(registerDto);
        user.Status = UserStatus.Active;
        await dbContext.SaveChangesAsync();

        var request = new LoginApiRequest(
            Identifier: "login_rest@nexus.local",
            Password: "Password123!"
        );

        var result = await controller.Login(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var resp = Assert.IsType<LoginApiResponse>(okResult.Value);
        Assert.Equal("mock-jwt-access-token-xyz", resp.AccessToken);
        Assert.Equal("Bearer", resp.TokenType);
        Assert.Equal(user.Id.ToString(), resp.User.Id);
        Assert.Equal("login_rest@nexus.local", resp.User.Email);
    }

    [Fact]
    public async Task Logout_Returns_Ok_With_Success_True()
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

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LogoutApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    private sealed class TestJwtTokenGenerator : IJwtTokenGenerator
    {
        public string GenerateToken(User user, System.Collections.Generic.IEnumerable<string> roles, out DateTime expiresAt)
        {
            expiresAt = DateTime.UtcNow.AddMinutes(60);
            return "mock-jwt-access-token-xyz";
        }
    }

    private sealed class TestRedisOtpService : IRedisOtpService
    {
        private readonly ConcurrentDictionary<string, OtpEntry> _storage = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, int> _loginAttempts = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTimeOffset> _lockouts = new(StringComparer.OrdinalIgnoreCase);

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

        public Task<int> IncrementLoginAttemptsAsync(string identifier, TimeSpan window)
        {
            var attempts = _loginAttempts.AddOrUpdate(identifier, 1, (_, current) => current + 1);
            return Task.FromResult(attempts);
        }

        public Task<bool> IsAccountLockedAsync(string identifier)
        {
            if (_lockouts.TryGetValue(identifier, out var expiresAt))
            {
                if (DateTimeOffset.UtcNow < expiresAt)
                {
                    return Task.FromResult(true);
                }
                _lockouts.TryRemove(identifier, out _);
            }
            return Task.FromResult(false);
        }

        public Task LockAccountAsync(string identifier, TimeSpan duration)
        {
            _lockouts[identifier] = DateTimeOffset.UtcNow.Add(duration);
            return Task.CompletedTask;
        }

        public Task ResetLoginAttemptsAsync(string identifier)
        {
            _loginAttempts.TryRemove(identifier, out _);
            _lockouts.TryRemove(identifier, out _);
            return Task.CompletedTask;
        }
    }
}
