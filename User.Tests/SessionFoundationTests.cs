namespace User.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Controllers;
using User.Application.Interfaces;
using User.Application.Models;
using User.Application.Services.Auth;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services.Jwt;
using User.Infrastructure.Services.Redis;
using Xunit;

public sealed class SessionFoundationTests
{
    private static AppDbContext CreateInMemoryDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? $"session_foundation_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Domain.Entities.User> SeedActiveBuyerAsync(AppDbContext dbContext, string email = "session.buyer@nexus.local")
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            RoleCode = RoleType.Buyer,
            RoleName = "Buyer",
            IsActive = true,
            IsSystemRole = true
        };
        dbContext.Roles.Add(role);

        var user = new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            FirstName = "Session",
            LastName = "Tester",
            Status = UserStatus.Active,
            RoleId = role.Id
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    [Fact]
    public void JwtTokenGenerator_defaults_to_15_minutes_and_embeds_sid_claim()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "http://localhost:8001",
                ["Jwt:Audience"] = "shoppingcart-api",
                ["Jwt:Signature"] = "super-secret-signing-key-with-at-least-32-bytes!"
            })
            .Build();

        var generator = new JwtTokenGenerator(config);
        var user = new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = "jwt.sid@nexus.local",
            PasswordHash = "hash",
            FirstName = "Jwt",
            LastName = "Tester",
            Status = UserStatus.Active,
            RoleId = Guid.NewGuid()
        };
        var sessionId = Guid.NewGuid();

        var tokenString = generator.GenerateToken(user, new[] { "buyer" }, Array.Empty<string>(), sessionId, out var expiresAt);

        var ttlMinutes = (expiresAt - DateTime.UtcNow).TotalMinutes;
        Assert.InRange(ttlMinutes, 14.0, 15.5);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);
        var sidClaim = jwt.Claims.FirstOrDefault(c => c.Type == "sid")?.Value;
        Assert.Equal(sessionId.ToString(), sidClaim);
    }

    [Fact]
    public async Task LoginAsync_and_RefreshTokenAsync_record_device_ip_and_preserve_session_id()
    {
        using var dbContext = CreateInMemoryDbContext();
        var user = await SeedActiveBuyerAsync(dbContext);
        var otpService = new TestRedisOtpService();
        var jwtGenerator = new CapturingSessionJwtTokenGenerator();
        var authService = new AuthService(dbContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);

        var longDevice = new string('A', 300);
        var loginResult = await authService.LoginAsync(new LoginDto(user.Email, "Password123!", longDevice, "203.0.113.10"));

        Assert.NotNull(jwtGenerator.LastSessionId);
        var initialSessionId = jwtGenerator.LastSessionId!.Value;
        Assert.NotEqual(Guid.Empty, initialSessionId);
        Assert.InRange(loginResult.ExpiresIn, 895, 900);

        var storedToken = await dbContext.RefreshTokens.SingleAsync(t => t.UserId == user.Id);
        Assert.Equal(initialSessionId, storedToken.SessionId);
        Assert.Equal(255, storedToken.DeviceInfo?.Length);
        Assert.Equal("203.0.113.10", storedToken.IpAddress);

        var refreshResult = await authService.RefreshTokenAsync(new RefreshTokenDto(
            loginResult.RefreshToken,
            DeviceInfo: "Chrome/129 Windows",
            IpAddress: "203.0.113.25"));

        Assert.Equal(initialSessionId, jwtGenerator.LastSessionId);
        Assert.NotEqual(loginResult.RefreshToken, refreshResult.RefreshToken);

        var tokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == user.Id)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, tokens.Count);
        Assert.True(tokens[0].IsRevoked);
        Assert.False(tokens[1].IsRevoked);
        Assert.Equal(initialSessionId, tokens[1].SessionId);
        Assert.Equal("Chrome/129 Windows", tokens[1].DeviceInfo);
        Assert.Equal("203.0.113.25", tokens[1].IpAddress);
    }

    [Fact]
    public async Task RefreshTokenAsync_concurrent_rotation_blocks_duplicate_session_and_reused_token_revokes_session()
    {
        var dbName = $"concurrent_rtr_db_{Guid.NewGuid():N}";
        using var setupContext = CreateInMemoryDbContext(dbName);
        var user = await SeedActiveBuyerAsync(setupContext);
        var otpService = new TestRedisOtpService();
        var jwtGenerator = new CapturingSessionJwtTokenGenerator();
        var setupAuth = new AuthService(setupContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);

        var login = await setupAuth.LoginAsync(new LoginDto(user.Email, "Password123!", "Firefox", "198.51.100.1"));
        var sessionId = jwtGenerator.LastSessionId!.Value;

        // Simulate two concurrent refresh requests with their own DbContext instances sharing the same DB
        var task1 = Task.Run(async () =>
        {
            using var ctx = CreateInMemoryDbContext(dbName);
            var svc = new AuthService(ctx, otpService, NullLogger<AuthService>.Instance, new CapturingSessionJwtTokenGenerator());
            return await svc.RefreshTokenAsync(new RefreshTokenDto(login.RefreshToken));
        });

        var task2 = Task.Run(async () =>
        {
            using var ctx = CreateInMemoryDbContext(dbName);
            var svc = new AuthService(ctx, otpService, NullLogger<AuthService>.Instance, new CapturingSessionJwtTokenGenerator());
            return await svc.RefreshTokenAsync(new RefreshTokenDto(login.RefreshToken));
        });

        var successes = 0;
        var failures = new List<DomainException>();

        foreach (var t in new[] { task1, task2 })
        {
            try
            {
                await t;
                successes++;
            }
            catch (DomainException ex)
            {
                failures.Add(ex);
            }
        }

        Assert.Equal(1, successes);
        Assert.Single(failures);
        Assert.Equal("REFRESH_TOKEN_REVOKED", failures[0].ErrorCode);

        // Reuse detection: replaying the revoked initial token revokes the entire session (including the newly rotated token)
        using var replayContext = CreateInMemoryDbContext(dbName);
        var replayAuth = new AuthService(replayContext, otpService, NullLogger<AuthService>.Instance, new CapturingSessionJwtTokenGenerator());
        var reuseEx = await Assert.ThrowsAsync<DomainException>(() =>
            replayAuth.RefreshTokenAsync(new RefreshTokenDto(login.RefreshToken)));
        Assert.Equal("REFRESH_TOKEN_REVOKED", reuseEx.ErrorCode);

        var activeInSession = await replayContext.RefreshTokens
            .CountAsync(t => t.SessionId == sessionId && !t.IsRevoked);
        Assert.Equal(0, activeInSession);
    }

    [Fact]
    public async Task AuthController_Login_extracts_UserAgent_and_Forwarded_Ip()
    {
        using var dbContext = CreateInMemoryDbContext();
        var user = await SeedActiveBuyerAsync(dbContext);
        var authService = new AuthService(dbContext, new TestRedisOtpService(), NullLogger<AuthService>.Instance, new CapturingSessionJwtTokenGenerator());

        var controller = new AuthController(authService, NullLogger<AuthController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "Mozilla/5.0 (Windows NT 10.0)";
        httpContext.Request.Headers["X-Forwarded-For"] = "203.0.113.55, 10.0.0.1";
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var response = await controller.Login(new LoginApiRequest(user.Email, "Password123!"), default);
        Assert.IsType<OkObjectResult>(response);

        var stored = await dbContext.RefreshTokens.SingleAsync(t => t.UserId == user.Id);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0)", stored.DeviceInfo);
        Assert.Equal("203.0.113.55", stored.IpAddress);
    }

    private sealed class CapturingSessionJwtTokenGenerator : IJwtTokenGenerator
    {
        public Guid? LastSessionId { get; private set; }

        public string GenerateToken(Domain.Entities.User user, IEnumerable<string> roles, IEnumerable<string>? permissions, out DateTime expiresAt)
            => GenerateToken(user, roles, permissions, null, out expiresAt);

        public string GenerateToken(Domain.Entities.User user, IEnumerable<string> roles, IEnumerable<string>? permissions, Guid? sessionId, out DateTime expiresAt)
        {
            LastSessionId = sessionId;
            expiresAt = DateTime.UtcNow.AddMinutes(15);
            return $"mock-jwt-{sessionId}";
        }
    }

    private sealed class TestRedisOtpService : IRedisOtpService
    {
        private readonly ConcurrentDictionary<string, OtpEntry> _storage = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, int> _loginAttempts = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTimeOffset> _lockouts = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTimeOffset> _cooldowns = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, int> _otpAttempts = new(StringComparer.OrdinalIgnoreCase);

        private static string Key(string email, string purpose) => $"{purpose}:{email}";

        public Task SaveOtpAsync(string email, string code, TimeSpan validityDuration, TimeSpan retentionDuration, string purpose = "registration")
        {
            _storage[Key(email, purpose)] = new OtpEntry { Code = code, Email = email, Status = OtpStatus.Active, CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.Add(validityDuration) };
            return Task.CompletedTask;
        }

        public Task<OtpEntry?> GetOtpAsync(string email, string purpose = "registration")
            => Task.FromResult(_storage.TryGetValue(Key(email, purpose), out var entry) ? entry : null);

        public Task MarkOtpUsedAsync(string email, TimeSpan retentionDuration, string purpose = "registration") => Task.CompletedTask;
        public Task MarkOtpConsumedAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task InvalidateExistingOtpsAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task DeleteOtpAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task<bool> IsInCooldownAsync(string email, string purpose = "registration") => Task.FromResult(false);
        public Task SetCooldownAsync(string email, TimeSpan cooldownDuration, string purpose = "registration") => Task.CompletedTask;
        public Task<int> IncrementOtpAttemptsAsync(string email, TimeSpan windowDuration, string purpose = "registration") => Task.FromResult(1);
        public Task<int> GetOtpAttemptsAsync(string email, string purpose = "registration") => Task.FromResult(0);
        public Task ResetOtpAttemptsAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task<int> IncrementLoginAttemptsAsync(string identifier, TimeSpan lockDuration) => Task.FromResult(_loginAttempts.AddOrUpdate(identifier, 1, (_, c) => c + 1));
        public Task ResetLoginAttemptsAsync(string identifier) { _loginAttempts.TryRemove(identifier, out _); return Task.CompletedTask; }
        public Task<bool> IsAccountLockedAsync(string identifier) => Task.FromResult(false);
        public Task LockAccountAsync(string identifier, TimeSpan lockDuration) => Task.CompletedTask;
        public Task SavePasswordResetOtpAsync(string email, string code, TimeSpan validityDuration) => SaveOtpAsync(email, code, validityDuration, TimeSpan.FromMinutes(10), "password_reset");
        public Task<OtpEntry?> GetPasswordResetOtpAsync(string email) => GetOtpAsync(email, "password_reset");
        public Task MarkPasswordResetOtpUsedAsync(string email, TimeSpan retentionDuration) => MarkOtpUsedAsync(email, retentionDuration, "password_reset");
        public Task DeletePasswordResetOtpAsync(string email) => DeleteOtpAsync(email, "password_reset");
        public Task<bool> IsPasswordResetInCooldownAsync(string email) => IsInCooldownAsync(email, "password_reset");
        public Task SetPasswordResetCooldownAsync(string email, TimeSpan cooldownDuration) => SetCooldownAsync(email, cooldownDuration, "password_reset");
        public Task<int> IncrementPasswordResetAttemptsAsync(string email, TimeSpan windowDuration) => IncrementOtpAttemptsAsync(email, windowDuration, "password_reset");
        public Task<int> GetPasswordResetAttemptsAsync(string email) => GetOtpAttemptsAsync(email, "password_reset");
        public Task ResetPasswordResetAttemptsAsync(string email) => ResetOtpAttemptsAsync(email, "password_reset");
    }
}
