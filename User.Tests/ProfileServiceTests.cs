namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Services.Profile;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class ProfileServiceTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"profile_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetProfileAsync_succeeds_when_user_exists()
    {
        using var dbContext = CreateInMemoryDbContext();
        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);

        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        var role = new Role
        {
            Id = roleId,
            RoleCode = RoleType.Seller,
            RoleName = "Seller",
            Description = "Seller Role"
        };
        dbContext.Roles.Add(role);

        var user = new Domain.Entities.User
        {
            Id = userId,
            Username = "seller_test",
            Email = "seller@nexus.local",
            PasswordHash = "hashed_pw",
            FirstName = "Van",
            LastName = "Nguyen",
            PhoneNumber = "0987654321",
            Address = "123 Le Loi, TP.HCM",
            Status = UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsDeleted = false
        };
        dbContext.Users.Add(user);

        dbContext.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            Role = role,
            AssignedAt = DateTimeOffset.UtcNow
        });

        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Nexus Official Store",
            BusinessEmail = "seller@nexus.local",
            PickupAddress = "123 Le Loi, TP.HCM",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "00001",
            Hotline = "19001234",
            Status = SellerProfileStatus.Approved,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 95,
            TrustLevel = TrustLevel.Excellent,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var profile = await profileService.GetProfileAsync(userId);

        Assert.NotNull(profile);
        Assert.Equal(userId.ToString(), profile.Id);
        Assert.Equal("seller_test", profile.Username);
        Assert.Equal("seller@nexus.local", profile.Email);
        Assert.Equal("Van Nguyen", profile.DisplayName);
        Assert.Equal("0987654321", profile.PhoneNumber);
        Assert.Equal("123 Le Loi, TP.HCM", profile.Address);
        Assert.Equal("Active", profile.Status);
        Assert.Contains("seller", profile.Roles);
        Assert.Equal("Nexus Official Store", profile.ShopName);
        Assert.Equal(95, profile.ReputationScore);
        Assert.Equal("Excellent", profile.TrustLevel);
    }

    [Fact]
    public async Task GetProfileAsync_throws_when_user_not_found()
    {
        using var dbContext = CreateInMemoryDbContext();
        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);

        var nonExistentId = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            profileService.GetProfileAsync(nonExistentId));

        Assert.Equal("USER_NOT_FOUND", ex.ErrorCode);
        Assert.Equal("User account not found.", ex.Message);
    }

    [Fact]
    public async Task GetProfileAsync_throws_when_user_id_is_empty()
    {
        using var dbContext = CreateInMemoryDbContext();
        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            profileService.GetProfileAsync(Guid.Empty));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
        Assert.Equal("User identifier is required.", ex.Message);
    }

    [Fact]
    public async Task GetProfileAsync_throws_when_user_is_deleted()
    {
        using var dbContext = CreateInMemoryDbContext();
        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "deleted@nexus.local",
            PasswordHash = "hashed_pw",
            FirstName = "Deleted",
            LastName = "User",
            Status = UserStatus.Active,
            IsDeleted = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            profileService.GetProfileAsync(userId));

        Assert.Equal("USER_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public async Task GetProfileAsync_returns_default_score_and_buyer_role_when_none_assigned()
    {
        using var dbContext = CreateInMemoryDbContext();
        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "simple_buyer@nexus.local",
            PasswordHash = "hashed_pw",
            FirstName = "Buyer",
            LastName = "Simple",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var profile = await profileService.GetProfileAsync(userId);

        Assert.NotNull(profile);
        Assert.Equal(50, profile.ReputationScore);
        Assert.Equal("Standard", profile.TrustLevel);
        Assert.Single(profile.Roles);
        Assert.Equal("buyer", profile.Roles[0]);
        Assert.Empty(profile.ShopName ?? string.Empty);
    }
}
