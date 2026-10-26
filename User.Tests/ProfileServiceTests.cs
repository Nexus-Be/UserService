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

    [Fact]
    public async Task UpdateProfileAsync_succeeds_and_updates_all_fields()
    {
        using var dbContext = CreateInMemoryDbContext();
        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "update_test@nexus.local",
            PasswordHash = "hashed_pw",
            FirstName = "OldFirst",
            LastName = "OldLast",
            PhoneNumber = "0900000000",
            Address = "Old Address",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var updateDto = new UpdateProfileDto(
            FirstName: "NewFirst",
            LastName: "NewLast",
            PhoneNumber: "0911222333",
            Address: "456 Nguyen Hue, TP.HCM",
            AvatarUrl: "https://nexus.local/avatars/user.png",
            Gender: Gender.Male,
            DateOfBirth: new DateTimeOffset(1995, 5, 20, 0, 0, 0, TimeSpan.Zero)
        );

        var result = await profileService.UpdateProfileAsync(userId, updateDto);

        Assert.NotNull(result);
        Assert.Equal("NewFirst", result.FirstName);
        Assert.Equal("NewLast", result.LastName);
        Assert.Equal("NewFirst NewLast", result.DisplayName);
        Assert.Equal("0911222333", result.PhoneNumber);
        Assert.Equal("456 Nguyen Hue, TP.HCM", result.Address);
        Assert.Equal("https://nexus.local/avatars/user.png", result.AvatarUrl);

        var dbUser = await dbContext.Users.FindAsync(userId);
        Assert.NotNull(dbUser);
        Assert.Equal("NewFirst", dbUser.FirstName);
        Assert.Equal("NewLast", dbUser.LastName);
        Assert.Equal("0911222333", dbUser.PhoneNumber);
        Assert.Equal(Gender.Male, dbUser.Gender);
        Assert.Equal(new DateTimeOffset(1995, 5, 20, 0, 0, 0, TimeSpan.Zero), dbUser.DateOfBirth);
    }

    [Fact]
    public async Task UpdateProfileAsync_partial_update_preserves_unchanged_fields()
    {
        using var dbContext = CreateInMemoryDbContext();
        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "partial@nexus.local",
            PasswordHash = "hashed_pw",
            FirstName = "OriginalFirst",
            LastName = "OriginalLast",
            PhoneNumber = "0988777666",
            Address = "Original Address",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var updateDto = new UpdateProfileDto(
            FirstName: "OnlyNewFirst"
        );

        var result = await profileService.UpdateProfileAsync(userId, updateDto);

        Assert.Equal("OnlyNewFirst", result.FirstName);
        Assert.Equal("OriginalLast", result.LastName);
        Assert.Equal("0988777666", result.PhoneNumber);
        Assert.Equal("Original Address", result.Address);
    }

    [Fact]
    public async Task UpdateProfileAsync_throws_when_user_not_found()
    {
        using var dbContext = CreateInMemoryDbContext();
        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);

        var nonExistentId = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            profileService.UpdateProfileAsync(nonExistentId, new UpdateProfileDto(FirstName: "Test")));

        Assert.Equal("USER_NOT_FOUND", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateProfileAsync_throws_when_user_id_is_empty()
    {
        using var dbContext = CreateInMemoryDbContext();
        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            profileService.UpdateProfileAsync(Guid.Empty, new UpdateProfileDto(FirstName: "Test")));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateProfileAsync_throws_when_phone_already_used_by_another_user()
    {
        using var dbContext = CreateInMemoryDbContext();
        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);

        var otherUser = new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = "other@nexus.local",
            PasswordHash = "hashed_pw",
            FirstName = "Other",
            LastName = "User",
            PhoneNumber = "0912345678",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(otherUser);

        var currentUser = new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = "current@nexus.local",
            PasswordHash = "hashed_pw",
            FirstName = "Current",
            LastName = "User",
            PhoneNumber = "0900000000",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(currentUser);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            profileService.UpdateProfileAsync(currentUser.Id, new UpdateProfileDto(PhoneNumber: "0912345678")));

        Assert.Equal("PHONE_ALREADY_EXISTS", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateProfileAsync_throws_when_first_name_exceeds_100_characters()
    {
        using var dbContext = CreateInMemoryDbContext();
        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "longname@nexus.local",
            PasswordHash = "hashed_pw",
            FirstName = "ValidFirst",
            LastName = "ValidLast",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var tooLongName = new string('A', 101);
        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            profileService.UpdateProfileAsync(userId, new UpdateProfileDto(FirstName: tooLongName)));

        Assert.Equal("VAL_INVALID_FIRST_NAME", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateProfileAsync_throws_when_address_exceeds_255_characters()
    {
        using var dbContext = CreateInMemoryDbContext();
        var profileService = new ProfileService(dbContext, NullLogger<ProfileService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "longaddress@nexus.local",
            PasswordHash = "hashed_pw",
            FirstName = "ValidFirst",
            LastName = "ValidLast",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var tooLongAddress = new string('B', 256);
        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            profileService.UpdateProfileAsync(userId, new UpdateProfileDto(Address: tooLongAddress)));

        Assert.Equal("VAL_INVALID_ADDRESS", ex.ErrorCode);
    }
}
