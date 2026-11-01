namespace User.Tests;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Interfaces;
using User.Application.Models;
using User.Application.Services.Auth;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services.Jwt;
using User.Infrastructure.Services.Redis;
using Xunit;

public sealed class PermissionDefectReproTests
{
    private sealed class CapturingJwtTokenGenerator : IJwtTokenGenerator
    {
        public List<string>? CapturedPermissions { get; private set; }

        public string GenerateToken(Domain.Entities.User user, IEnumerable<string> roles, IEnumerable<string>? permissions, out DateTime expiresAt)
        {
            CapturedPermissions = permissions?.ToList();
            expiresAt = DateTime.UtcNow.AddMinutes(60);
            return "mock-access-token";
        }
    }

    [Fact]
    public async Task RoleOnly_Login_And_RefreshToken_Do_Not_Generate_Permissions_Or_Fallback()
    {
        // ARRANGE: Set up a shared in-memory database
        var dbName = $"repro_db_{Guid.NewGuid():N}";
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var customRole = new Role
        {
            Id = Guid.NewGuid(),
            RoleCode = RoleType.Buyer,
            RoleName = "buyer"
        };
        var customPerm = new Permission
        {
            Id = Guid.NewGuid(),
            PermissionCode = "SPECIAL.REPORT.EXPORT",
            PermissionName = "Special Report Export",
            Category = "Report"
        };
        var rolePerm = new RolePermission
        {
            RoleId = customRole.Id,
            Role = customRole,
            PermissionId = customPerm.Id,
            Permission = customPerm
        };

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "defect_test@nexus.local",
            FirstName = "Defect",
            LastName = "Test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            Status = UserStatus.Active,
            UserRoles = new List<UserRole>
            {
                new() { UserId = userId, RoleId = customRole.Id, Role = customRole }
            }
        };

        using (var setupContext = new AppDbContext(dbOptions))
        {
            setupContext.Roles.Add(customRole);
            setupContext.Permissions.Add(customPerm);
            setupContext.RolePermissions.Add(rolePerm);
            setupContext.Users.Add(user);
            await setupContext.SaveChangesAsync();
        }

        var otpService = new TestRedisOtpService();
        var jwtGenerator = new CapturingJwtTokenGenerator();

        // 1. Perform Login with a fresh DbContext
        string refreshToken;
        using (var loginContext = new AppDbContext(dbOptions))
        {
            var authService = new AuthService(loginContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);
            var loginResult = await authService.LoginAsync(new LoginDto("defect_test@nexus.local", "Password123!"));
            refreshToken = loginResult.RefreshToken!;

            // Verify login operates in Role-only mode: empty permissions, valid roles
            Assert.NotNull(jwtGenerator.CapturedPermissions);
            Assert.Empty(jwtGenerator.CapturedPermissions!);
            Assert.Empty(loginResult.Permissions!);
            Assert.Contains("buyer", loginResult.Roles!);
        }

        // 2. Perform RefreshToken with a fresh DbContext (simulating the next HTTP request)
        using (var refreshContext = new AppDbContext(dbOptions))
        {
            var authService = new AuthService(refreshContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);
            var refreshResult = await authService.RefreshTokenAsync(new RefreshTokenDto(refreshToken));

            // Verify refresh operates in Role-only mode: empty permissions, valid roles
            Assert.NotNull(jwtGenerator.CapturedPermissions);
            Assert.Empty(jwtGenerator.CapturedPermissions!);
            Assert.Empty(refreshResult.Permissions!);
            Assert.Contains("buyer", refreshResult.Roles!);
        }
    }

    [Fact]
    public async Task RoleOnly_Login_Extracts_Roles_Properly_Without_Permissions()
    {
        var dbName = $"repro_untrimmed_db_{Guid.NewGuid():N}";
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var role1 = new Role { Id = Guid.NewGuid(), RoleCode = RoleType.Buyer, RoleName = "buyer" };
        var role2 = new Role { Id = Guid.NewGuid(), RoleCode = RoleType.Seller, RoleName = "seller" };

        var perm1 = new Permission { Id = Guid.NewGuid(), PermissionCode = "ORDER.VIEW ", PermissionName = "Order View Padded", Category = "Order" };
        var perm2 = new Permission { Id = Guid.NewGuid(), PermissionCode = "ORDER.VIEW", PermissionName = "Order View Clean", Category = "Order" };

        var rp1 = new RolePermission { RoleId = role1.Id, Role = role1, PermissionId = perm1.Id, Permission = perm1 };
        var rp2 = new RolePermission { RoleId = role2.Id, Role = role2, PermissionId = perm2.Id, Permission = perm2 };

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "trim_test@nexus.local",
            FirstName = "Trim",
            LastName = "Test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            Status = UserStatus.Active,
            UserRoles = new List<UserRole>
            {
                new() { UserId = userId, RoleId = role1.Id, Role = role1 },
                new() { UserId = userId, RoleId = role2.Id, Role = role2 }
            }
        };

        using (var setupContext = new AppDbContext(dbOptions))
        {
            setupContext.Roles.AddRange(role1, role2);
            setupContext.Permissions.AddRange(perm1, perm2);
            setupContext.RolePermissions.AddRange(rp1, rp2);
            setupContext.Users.Add(user);
            await setupContext.SaveChangesAsync();
        }

        var otpService = new TestRedisOtpService();
        var jwtGenerator = new CapturingJwtTokenGenerator();

        using (var loginContext = new AppDbContext(dbOptions))
        {
            var authService = new AuthService(loginContext, otpService, NullLogger<AuthService>.Instance, jwtGenerator);
            var loginResult = await authService.LoginAsync(new LoginDto("trim_test@nexus.local", "Password123!"));

            // INTENT: Login extracts multiple roles accurately while permissions are cleanly empty
            Assert.Contains("buyer", loginResult.Roles!);
            Assert.Contains("seller", loginResult.Roles!);
            Assert.NotNull(jwtGenerator.CapturedPermissions);
            Assert.Empty(jwtGenerator.CapturedPermissions!);
            Assert.Empty(loginResult.Permissions!);
        }
    }

    [Fact]
    public void Defect_JwtTokenGenerator_Produces_Duplicate_Claims_When_Given_Duplicate_Or_MixedCase_Permissions()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "http://localhost:8001",
                ["Jwt:Audience"] = "shoppingcart-api",
                ["Jwt:SecretKey"] = "TestSignatureKeyWithAtLeast32CharactersLong!"
            })
            .Build();

        var generator = new JwtTokenGenerator(config);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "dup_test@nexus.local",
            FirstName = "Dup",
            LastName = "Test",
            PasswordHash = "hash",
            Status = UserStatus.Active
        };

        // Two identical permissions (or same permission with different casing/whitespace)
        var permissions = new[] { "PRODUCT.CREATE", "product.create" };

        var tokenString = generator.GenerateToken(user, new[] { "buyer" }, permissions, out _);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        // INTENT: Permissions injected into the token should be deduplicated.
        // BUG: JwtTokenGenerator does not deduplicate and emits duplicate claims for each item.
        var permissionClaims = jwt.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();
        Assert.Single(permissionClaims);
    }

    private sealed class TestRedisOtpService : IRedisOtpService
    {
        public Task SaveOtpAsync(string email, string code, TimeSpan validityDuration, TimeSpan retentionDuration, string purpose = "registration") => Task.CompletedTask;
        public Task<OtpEntry?> GetOtpAsync(string email, string purpose = "registration") => Task.FromResult<OtpEntry?>(null);
        public Task InvalidateExistingOtpsAsync(string email, string purpose = "registration") => Task.CompletedTask;
        public Task<int> IncrementLoginAttemptsAsync(string email, TimeSpan window) => Task.FromResult(0);
        public Task ResetLoginAttemptsAsync(string email) => Task.CompletedTask;
        public Task LockAccountAsync(string email, TimeSpan duration) => Task.CompletedTask;
        public Task<bool> IsAccountLockedAsync(string email) => Task.FromResult(false);
        public Task MarkOtpConsumedAsync(string email, string purpose = "registration") => Task.CompletedTask;
    }
}
