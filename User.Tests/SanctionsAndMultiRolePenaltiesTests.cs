using User.Domain.Exceptions;
namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Services.Admin;
using User.Application.Services.Seller;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class SanctionsAndMultiRolePenaltiesTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"sanctions_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task BanUserAsync_WhenUserIsAlsoSeller_BansAccountAndSuspendsShopAndCreatesPenalty()
    {
        using var dbContext = CreateInMemoryDbContext();
        var adminService = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Arrange: User with an approved seller profile
        var user = new User
        {
            Id = userId,
            Email = "seller@nexus.local",
            PasswordHash = "hash",
            FirstName = "Shop",
            LastName = "Owner",
            Status = UserStatus.Active,
            EmailVerifiedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);

        var sellerProfile = new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Nexus Electronics",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "W1",
            Hotline = "0900000000",
            Status = SellerProfileStatus.Approved
        };
        dbContext.SellerProfiles.Add(sellerProfile);
        await dbContext.SaveChangesAsync();

        // Act: Ban user account
        var banDto = new BanAdminUserDto
        {
            Reason = "Severe counterfeit fraud violation",
            PolicyClauseCode = "TOS_FRAUD_4_1"
        };
        var banResult = await adminService.BanUserAsync(userId, banDto, adminId);

        // Assert
        Assert.NotNull(banResult);
        Assert.Equal("Banned", banResult.Status);

        var updatedUser = await dbContext.Users.Include(u => u.SellerProfile).FirstAsync(u => u.Id == userId);
        Assert.Equal(UserStatus.Banned, updatedUser.Status);
        Assert.NotNull(updatedUser.SellerProfile);
        Assert.Equal(SellerProfileStatus.Suspended, updatedUser.SellerProfile.Status);

        // Verify ledger record in UserPenalties
        var penalty = await dbContext.UserPenalties.FirstOrDefaultAsync(p => p.UserId == userId);
        Assert.NotNull(penalty);
        Assert.Equal(TargetRole.Account, penalty.TargetRole);
        Assert.Equal(PenaltyType.BanAccount, penalty.PenaltyApplied);
        Assert.Equal(PenaltyStatus.Active, penalty.Status);
        Assert.Equal("TOS_FRAUD_4_1", penalty.PolicyClauseCode);
    }

    [Fact]
    public async Task SuspendSellerAsync_SuspendsOnlyShop_UserRemainsActiveBuyer()
    {
        using var dbContext = CreateInMemoryDbContext();
        var sellerService = new SellerService(
            dbContext,
            otpService: null!,
            emailNotificationClient: null,
            fulfillmentClient: null!,
            NullLogger<SellerService>.Instance);

        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Email = "dualrole@nexus.local",
            PasswordHash = "hash",
            FirstName = "Dual",
            LastName = "Role",
            Status = UserStatus.Active,
            EmailVerifiedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);

        var sellerProfile = new SellerProfile
        {
            Id = sellerId,
            UserId = userId,
            ShopName = "Dual Role Shop",
            PickupAddress = "456 Street",
            ProvinceId = 2,
            DistrictId = 2,
            WardCode = "W2",
            Hotline = "0911111111",
            Status = SellerProfileStatus.Approved
        };
        dbContext.SellerProfiles.Add(sellerProfile);
        await dbContext.SaveChangesAsync();

        // Act: Admin suspends only the seller shop for 7 days
        var suspendDto = new SuspendSellerDto
        {
            Reason = "Late fulfillment rate exceeded threshold",
            DurationDays = 7,
            PolicyClauseCode = "SLA_LATE_SHIPMENT"
        };
        var result = await sellerService.SuspendSellerAsync(sellerId, suspendDto, adminId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SellerProfileStatus.Suspended.ToString(), result.Status);
        Assert.NotNull(result.ExpiresAt);

        // Shop is suspended
        var updatedProfile = await dbContext.SellerProfiles.FindAsync(sellerId);
        Assert.NotNull(updatedProfile);
        Assert.Equal(SellerProfileStatus.Suspended, updatedProfile.Status);

        // But user remains Active! (Can still buy)
        var updatedUser = await dbContext.Users.FindAsync(userId);
        Assert.NotNull(updatedUser);
        Assert.Equal(UserStatus.Active, updatedUser.Status);

        // Penalty recorded with TargetRole = Seller
        var penalty = await dbContext.UserPenalties.FirstOrDefaultAsync(p => p.UserId == userId);
        Assert.NotNull(penalty);
        Assert.Equal(TargetRole.Seller, penalty.TargetRole);
        Assert.Equal(PenaltyType.Restriction, penalty.PenaltyApplied);
        Assert.Equal(PenaltyStatus.Active, penalty.Status);
        Assert.NotNull(penalty.ExpiresAt);
    }

    [Fact]
    public async Task ReactivateSellerAsync_RestoresShopToApproved_AndRevokesSellerPenalty()
    {
        using var dbContext = CreateInMemoryDbContext();
        var sellerService = new SellerService(
            dbContext,
            otpService: null!,
            emailNotificationClient: null,
            fulfillmentClient: null!,
            NullLogger<SellerService>.Instance);

        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Email = "appeal@nexus.local",
            PasswordHash = "hash",
            FirstName = "Appealing",
            LastName = "Seller",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);

        var sellerProfile = new SellerProfile
        {
            Id = sellerId,
            UserId = userId,
            ShopName = "Appealing Shop",
            PickupAddress = "789 Street",
            ProvinceId = 3,
            DistrictId = 3,
            WardCode = "W3",
            Hotline = "0922222222",
            Status = SellerProfileStatus.Suspended
        };
        dbContext.SellerProfiles.Add(sellerProfile);

        var penalty = new UserPenalty
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetRole = TargetRole.Seller,
            ViolationType = PenaltyViolationType.Other,
            Reason = "Suspended for review",
            PenaltyApplied = PenaltyType.Restriction,
            Status = PenaltyStatus.Active
        };
        dbContext.UserPenalties.Add(penalty);
        await dbContext.SaveChangesAsync();

        // Act: Reactivate shop
        var reactivateResult = await sellerService.ReactivateSellerAsync(sellerId, new ReactivateSellerDto { Reason = "Appeal accepted" }, adminId);

        // Assert
        Assert.NotNull(reactivateResult);
        Assert.Equal(SellerProfileStatus.Approved.ToString(), reactivateResult.Status);

        var updatedProfile = await dbContext.SellerProfiles.FindAsync(sellerId);
        Assert.NotNull(updatedProfile);
        Assert.Equal(SellerProfileStatus.Approved, updatedProfile.Status);

        var updatedPenalty = await dbContext.UserPenalties.FindAsync(penalty.Id);
        Assert.NotNull(updatedPenalty);
        Assert.Equal(PenaltyStatus.Revoked, updatedPenalty.Status);
        Assert.Equal("Appeal accepted", updatedPenalty.RevokeReason);
    }

    [Fact]
    public async Task RevokePenaltyAsync_ViaAdminPenaltyService_RestoresOperationalState()
    {
        using var dbContext = CreateInMemoryDbContext();
        var penaltyService = new AdminPenaltyService(dbContext, NullLogger<AdminPenaltyService>.Instance);

        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var penaltyId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Email = "revoke@nexus.local",
            PasswordHash = "hash",
            FirstName = "Revoke",
            LastName = "Test",
            Status = UserStatus.Banned,
            EmailVerifiedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);

        var penalty = new UserPenalty
        {
            Id = penaltyId,
            UserId = userId,
            TargetRole = TargetRole.Account,
            ViolationType = PenaltyViolationType.Fraud,
            Reason = "Suspected fraud",
            PenaltyApplied = PenaltyType.BanAccount,
            Status = PenaltyStatus.Active
        };
        dbContext.UserPenalties.Add(penalty);
        await dbContext.SaveChangesAsync();

        // Act: Revoke the account ban penalty
        var result = await penaltyService.RevokePenaltyAsync(penaltyId, new RevokePenaltyDto { Reason = "Identity verified upon appeal" }, adminId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PenaltyStatus.Revoked, result.Status);
        Assert.Equal("AccountRestored", result.RestoredOperationalState);

        var restoredUser = await dbContext.Users.FindAsync(userId);
        Assert.NotNull(restoredUser);
        Assert.Equal(UserStatus.Active, restoredUser.Status);
        Assert.Null(restoredUser.BannedAt);
    }

    [Fact]
    public async Task CheckUserActionPermissionAsync_AccuratelyDifferentiatesBuyAndSell()
    {
        using var dbContext = CreateInMemoryDbContext();
        var penaltyService = new AdminPenaltyService(dbContext, NullLogger<AdminPenaltyService>.Instance);

        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Email = "permissions@nexus.local",
            PasswordHash = "hash",
            FirstName = "Action",
            LastName = "Check",
            Status = UserStatus.Active,
            EmailVerifiedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);

        // Shop is suspended
        var sellerProfile = new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Suspended Shop",
            PickupAddress = "Road 1",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "W1",
            Hotline = "0933333333",
            Status = SellerProfileStatus.Suspended,
            RejectionReason = "Policy violation"
        };
        dbContext.SellerProfiles.Add(sellerProfile);

        // Active seller penalty
        dbContext.UserPenalties.Add(new UserPenalty
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetRole = TargetRole.Seller,
            ViolationType = PenaltyViolationType.Other,
            Reason = "Policy violation",
            PenaltyApplied = PenaltyType.Restriction,
            Status = PenaltyStatus.Active
        });
        await dbContext.SaveChangesAsync();

        // Check BUY: should be allowed (user is active, no buyer restriction)
        var buyCheck = await penaltyService.CheckUserActionPermissionAsync(userId, "buy");
        Assert.True(buyCheck.IsAllowed);

        // Check SELL: should be DISALLOWED (shop is suspended)
        var sellCheck = await penaltyService.CheckUserActionPermissionAsync(userId, "sell");
        Assert.False(sellCheck.IsAllowed);
        Assert.Contains("suspended", sellCheck.Reason, StringComparison.OrdinalIgnoreCase);

        // Check LOGIN: should be allowed
        var loginCheck = await penaltyService.CheckUserActionPermissionAsync(userId, "login");
        Assert.True(loginCheck.IsAllowed);
    }

    [Fact]
    public async Task UnbanUser_WhenIndependentSellerPenaltyStillActive_ShouldNOTReactivateShop()
    {
        using var dbContext = CreateInMemoryDbContext();
        var adminService = new AdminUserService(dbContext, NullLogger<AdminUserService>.Instance);

        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Email = "dualban@nexus.local",
            PasswordHash = "hash",
            FirstName = "Dual",
            LastName = "Ban",
            Status = UserStatus.Banned,
            EmailVerifiedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);

        var sellerProfile = new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Independent Violation Shop",
            PickupAddress = "Street 9",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "W1",
            Hotline = "0944444444",
            Status = SellerProfileStatus.Suspended
        };
        dbContext.SellerProfiles.Add(sellerProfile);

        // Account penalty (will be unbanned)
        dbContext.UserPenalties.Add(new UserPenalty
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetRole = TargetRole.Account,
            ViolationType = PenaltyViolationType.Fraud,
            Reason = "Account ban",
            PenaltyApplied = PenaltyType.BanAccount,
            Status = PenaltyStatus.Active
        });

        // Independent active seller penalty (should KEEP shop suspended)
        dbContext.UserPenalties.Add(new UserPenalty
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetRole = TargetRole.Seller,
            ViolationType = PenaltyViolationType.Other,
            Reason = "Late shipment violation",
            PenaltyApplied = PenaltyType.Restriction,
            Status = PenaltyStatus.Active,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(10)
        });
        await dbContext.SaveChangesAsync();

        // Act: Unban account
        var unbanResult = await adminService.UnbanUserAsync(userId, new UnbanAdminUserDto { Reason = "Unbanned" }, adminId);

        // Assert: User account is unbanned to Active
        Assert.NotNull(unbanResult);
        Assert.Equal("Active", unbanResult.Status);

        // BUT Seller Profile MUST REMAIN Suspended because of independent seller penalty!
        var updatedProfile = await dbContext.SellerProfiles.FirstAsync(sp => sp.UserId == userId);
        Assert.Equal(SellerProfileStatus.Suspended, updatedProfile.Status);
    }

    [Fact]
    public async Task ReactivateSeller_WhenUserAccountIsBanned_ShouldThrowDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var sellerService = new SellerService(
            dbContext,
            otpService: null!,
            emailNotificationClient: null,
            fulfillmentClient: null!,
            NullLogger<SellerService>.Instance);

        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Email = "bannedowner@nexus.local",
            PasswordHash = "hash",
            FirstName = "Banned",
            LastName = "Owner",
            Status = UserStatus.Banned
        };
        dbContext.Users.Add(user);

        var sellerProfile = new SellerProfile
        {
            Id = sellerId,
            UserId = userId,
            ShopName = "Banned Owner Shop",
            PickupAddress = "Street 10",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "W1",
            Hotline = "0955555555",
            Status = SellerProfileStatus.Suspended
        };
        dbContext.SellerProfiles.Add(sellerProfile);
        await dbContext.SaveChangesAsync();

        // Act & Assert: Reactivating shop of banned account must fail
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            sellerService.ReactivateSellerAsync(sellerId, new ReactivateSellerDto { Reason = "Trying to reactivate" }, adminId));

        Assert.Equal("CANNOT_REACTIVATE_BANNED_USER_SHOP", ex.ErrorCode);
    }
}