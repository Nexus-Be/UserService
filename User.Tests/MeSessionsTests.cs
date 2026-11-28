namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Controllers;
using User.Application.Common.Models;
using User.Application.Services.Profile;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class MeSessionsTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"me_sessions_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Domain.Entities.User> SeedUserAsync(AppDbContext dbContext, string email)
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
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User",
            Status = UserStatus.Active,
            RoleId = role.Id
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task GetActiveSessionsAsync_returns_paginated_active_sessions_and_marks_isCurrent()
    {
        using var dbContext = CreateInMemoryDbContext();
        var user = await SeedUserAsync(dbContext, "sessions.user@nexus.local");
        var otherUser = await SeedUserAsync(dbContext, "other.user@nexus.local");

        var currentSid = Guid.NewGuid();
        var otherDeviceSid = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        dbContext.RefreshTokens.AddRange(
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SessionId = currentSid,
                TokenHash = "hash-current",
                DeviceInfo = "Chrome Windows",
                IpAddress = "203.0.113.10",
                IsRevoked = false,
                ExpiresAt = now.AddDays(7),
                CreatedAt = now.AddMinutes(-10),
                UpdatedAt = now.AddMinutes(-1)
            },
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SessionId = otherDeviceSid,
                TokenHash = "hash-mobile",
                DeviceInfo = "iOS App",
                IpAddress = "203.0.113.20",
                IsRevoked = false,
                ExpiresAt = now.AddDays(5),
                CreatedAt = now.AddHours(-2),
                UpdatedAt = now.AddHours(-1)
            },
            // Revoked token — must be excluded
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SessionId = Guid.NewGuid(),
                TokenHash = "hash-revoked",
                IsRevoked = true,
                ExpiresAt = now.AddDays(7),
                RevokedAt = now.AddMinutes(-5)
            },
            // Expired token — must be excluded
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SessionId = Guid.NewGuid(),
                TokenHash = "hash-expired",
                IsRevoked = false,
                ExpiresAt = now.AddMinutes(-1)
            },
            // Another user's session — must be excluded
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = otherUser.Id,
                SessionId = Guid.NewGuid(),
                TokenHash = "hash-other-user",
                IsRevoked = false,
                ExpiresAt = now.AddDays(7)
            });
        await dbContext.SaveChangesAsync();

        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);

        var page1 = await profileService.GetActiveSessionsAsync(user.Id, currentSid, page: 1, pageSize: 1);
        Assert.Equal(2, page1.Pagination.TotalCount);
        Assert.Equal(2, page1.Pagination.TotalPages);
        Assert.True(page1.Pagination.HasNextPage);
        Assert.Single(page1.Items);
        Assert.Equal(currentSid, page1.Items[0].SessionId);
        Assert.True(page1.Items[0].IsCurrent);
        Assert.Equal("Chrome Windows", page1.Items[0].DeviceInfo);

        var page2 = await profileService.GetActiveSessionsAsync(user.Id, currentSid, page: 2, pageSize: 1);
        Assert.Single(page2.Items);
        Assert.Equal(otherDeviceSid, page2.Items[0].SessionId);
        Assert.False(page2.Items[0].IsCurrent);
    }

    [Fact]
    public async Task RevokeSessionAsync_revokes_own_session_and_blocks_other_users_or_missing()
    {
        using var dbContext = CreateInMemoryDbContext();
        var owner = await SeedUserAsync(dbContext, "owner@nexus.local");
        var attacker = await SeedUserAsync(dbContext, "attacker@nexus.local");
        var ownerSid = Guid.NewGuid();

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = owner.Id,
            SessionId = ownerSid,
            TokenHash = "hash-owner",
            IsRevoked = false,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        });
        await dbContext.SaveChangesAsync();

        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);

        // Missing session -> 404 SESSION_NOT_FOUND
        var notFoundEx = await Assert.ThrowsAsync<DomainException>(() =>
            profileService.RevokeSessionAsync(owner.Id, Guid.NewGuid()));
        Assert.Equal(404, notFoundEx.StatusCode);
        Assert.Equal("SESSION_NOT_FOUND", notFoundEx.ErrorCode);

        // Another user -> 403 FORBIDDEN
        var forbiddenEx = await Assert.ThrowsAsync<DomainException>(() =>
            profileService.RevokeSessionAsync(attacker.Id, ownerSid));
        Assert.Equal(403, forbiddenEx.StatusCode);
        Assert.Equal("FORBIDDEN", forbiddenEx.ErrorCode);

        // Owner revokes -> success
        await profileService.RevokeSessionAsync(owner.Id, ownerSid);

        var activeAfter = await profileService.GetActiveSessionsAsync(owner.Id, ownerSid);
        Assert.Empty(activeAfter.Items);
    }

    [Fact]
    public async Task MeController_GetActiveSessions_and_RevokeSession_return_expected_http_responses()
    {
        using var dbContext = CreateInMemoryDbContext();
        var user = await SeedUserAsync(dbContext, "controller.sessions@nexus.local");
        var sid = Guid.NewGuid();

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SessionId = sid,
            TokenHash = "hash-ctrl",
            DeviceInfo = "Edge",
            IpAddress = "198.51.100.7",
            IsRevoked = false,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        });
        await dbContext.SaveChangesAsync();

        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);
        var controller = new MeController(profileService, NullLogger<MeController>.Instance);

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("sid", sid.ToString())
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        var getResponse = await controller.GetActiveSessions();
        var okResult = Assert.IsType<OkObjectResult>(getResponse);
        var paged = Assert.IsType<PagedData<ActiveSessionDto>>(okResult.Value);
        Assert.Single(paged.Items);
        Assert.True(paged.Items[0].IsCurrent);
        Assert.Equal(sid, paged.Items[0].SessionId);

        var deleteResponse = await controller.RevokeSession(sid);
        Assert.IsType<NoContentResult>(deleteResponse);
    }
}
