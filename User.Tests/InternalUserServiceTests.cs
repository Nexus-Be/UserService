namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Services.Internal;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class InternalUserServiceTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"internal_user_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task VerifyBidEligibilityAsync_WhenUserIsActiveWithScoreAtLeast50_ReturnsAllowedTrue()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new InternalUserService(dbContext, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "buyer@nexus.local",
            PasswordHash = "hash",
            FirstName = "Van",
            LastName = "Nguyen",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);
        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 75
        });
        await dbContext.SaveChangesAsync();

        var response = await service.VerifyBidEligibilityAsync(new VerifyBidEligibilityRequest(userId), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(userId.ToString(), response.UserId);
        Assert.True(response.Allowed);
        Assert.Null(response.FailureReason);
    }

    [Fact]
    public async Task VerifyBidEligibilityAsync_WhenUserNotFound_ReturnsErrUserNotFound()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new InternalUserService(dbContext, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        var response = await service.VerifyBidEligibilityAsync(new VerifyBidEligibilityRequest(userId), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(userId.ToString(), response.UserId);
        Assert.False(response.Allowed);
        Assert.Equal("ERR_USER_NOT_FOUND", response.FailureReason);
    }

    [Fact]
    public async Task VerifyBidEligibilityAsync_WhenUserBanned_ReturnsErrAccountBanned()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new InternalUserService(dbContext, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "banned@nexus.local",
            PasswordHash = "hash",
            FirstName = "Banned",
            LastName = "User",
            Status = UserStatus.Banned
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var response = await service.VerifyBidEligibilityAsync(new VerifyBidEligibilityRequest(userId), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(userId.ToString(), response.UserId);
        Assert.False(response.Allowed);
        Assert.Equal("ERR_ACCOUNT_BANNED", response.FailureReason);
    }

    [Fact]
    public async Task VerifyBidEligibilityAsync_WhenUserPendingVerification_ReturnsErrAccountUnverified()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new InternalUserService(dbContext, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "unverified@nexus.local",
            PasswordHash = "hash",
            FirstName = "Unverified",
            LastName = "User",
            Status = UserStatus.PendingVerification
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var response = await service.VerifyBidEligibilityAsync(new VerifyBidEligibilityRequest(userId), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(userId.ToString(), response.UserId);
        Assert.False(response.Allowed);
        Assert.Equal("ERR_ACCOUNT_UNVERIFIED", response.FailureReason);
    }

    [Fact]
    public async Task VerifyBidEligibilityAsync_WhenReputationScoreBelow50_ReturnsErrReputationTooLow()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new InternalUserService(dbContext, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "lowrep@nexus.local",
            PasswordHash = "hash",
            FirstName = "Low",
            LastName = "Rep",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);
        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 40
        });
        await dbContext.SaveChangesAsync();

        var response = await service.VerifyBidEligibilityAsync(new VerifyBidEligibilityRequest(userId), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(userId.ToString(), response.UserId);
        Assert.False(response.Allowed);
        Assert.Equal("ERR_REPUTATION_TOO_LOW", response.FailureReason);
    }

    [Fact]
    public async Task VerifyBidEligibilityAsync_WhenReputationRecordNull_DefaultsTo50AndAllows()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new InternalUserService(dbContext, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "defaultrep@nexus.local",
            PasswordHash = "hash",
            FirstName = "Default",
            LastName = "Rep",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var response = await service.VerifyBidEligibilityAsync(new VerifyBidEligibilityRequest(userId), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(userId.ToString(), response.UserId);
        Assert.True(response.Allowed);
        Assert.Null(response.FailureReason);
    }

    [Fact]
    public async Task VerifyBidEligibilityAsync_WhenActiveTemporaryPenaltyExists_ReturnsErrAccountLockedTemporary()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new InternalUserService(dbContext, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "locked@nexus.local",
            PasswordHash = "hash",
            FirstName = "Locked",
            LastName = "User",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);
        dbContext.UserPenalties.Add(new UserPenalty
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetRole = TargetRole.Account,
            ViolationType = PenaltyViolationType.AuctionNonPayment,
            PenaltyApplied = PenaltyType.Restriction,
            Reason = "Temporary lock for testing",
            Status = PenaltyStatus.Active,
            IssuedAt = DateTimeOffset.UtcNow.AddHours(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(2)
        });
        await dbContext.SaveChangesAsync();

        var response = await service.VerifyBidEligibilityAsync(new VerifyBidEligibilityRequest(userId), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(userId.ToString(), response.UserId);
        Assert.False(response.Allowed);
        Assert.Equal("ERR_ACCOUNT_LOCKED_TEMPORARY", response.FailureReason);
    }

    [Fact]
    public async Task CheckReputationAsync_WhenApprovedSellerWithScoreAtLeast50_ReturnsAllowedTrueAndIsApprovedSellerTrue()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new InternalUserService(dbContext, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "seller@nexus.local",
            PasswordHash = "hash",
            FirstName = "Seller",
            LastName = "User",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Test Shop",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Hotline = "0901234567",
            Status = SellerProfileStatus.Approved
        });
        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 80
        });
        await dbContext.SaveChangesAsync();

        var response = await service.CheckReputationAsync(new CheckReputationRequest("SELL", userId), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(userId.ToString(), response.UserId);
        Assert.True(response.Allowed);
        Assert.True(response.IsApprovedSeller);
        Assert.Equal("Active", response.UserStatus);
        Assert.Null(response.FailureReason);
    }

    [Fact]
    public async Task CheckReputationAsync_WhenUserNotFound_ReturnsErrUserNotFoundAndIsApprovedSellerFalse()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new InternalUserService(dbContext, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        var response = await service.CheckReputationAsync(new CheckReputationRequest("SELL", userId), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(userId.ToString(), response.UserId);
        Assert.False(response.Allowed);
        Assert.False(response.IsApprovedSeller);
        Assert.Equal("ERR_USER_NOT_FOUND", response.FailureReason);
    }

    [Fact]
    public async Task CheckReputationAsync_WhenSellerNotApproved_ReturnsErrAccountUnverifiedAndIsApprovedSellerFalse()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new InternalUserService(dbContext, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "pending_seller@nexus.local",
            PasswordHash = "hash",
            FirstName = "Pending",
            LastName = "Seller",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Pending Shop",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Hotline = "0901234567",
            Status = SellerProfileStatus.Pending
        });
        await dbContext.SaveChangesAsync();

        var response = await service.CheckReputationAsync(new CheckReputationRequest("SELL", userId), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(userId.ToString(), response.UserId);
        Assert.False(response.Allowed);
        Assert.False(response.IsApprovedSeller);
        Assert.Equal("ERR_ACCOUNT_UNVERIFIED", response.FailureReason);
    }

    [Fact]
    public async Task CheckReputationAsync_WhenReputationBelow50_ReturnsErrReputationTooLow()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new InternalUserService(dbContext, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "lowrep_seller@nexus.local",
            PasswordHash = "hash",
            FirstName = "LowRep",
            LastName = "Seller",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "LowRep Shop",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Hotline = "0901234567",
            Status = SellerProfileStatus.Approved
        });
        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 35
        });
        await dbContext.SaveChangesAsync();

        var response = await service.CheckReputationAsync(new CheckReputationRequest("SELL", userId), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(userId.ToString(), response.UserId);
        Assert.False(response.Allowed);
        Assert.False(response.IsApprovedSeller);
        Assert.Equal("ERR_REPUTATION_TOO_LOW", response.FailureReason);
    }

    [Fact]
    public async Task GetBatchPublicInfoAsync_WhenUsersExist_ReturnsMaskedNamesWithIsDeletedFalse()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new InternalUserService(dbContext, NullLogger<InternalUserService>.Instance);

        var user1Id = Guid.NewGuid();
        var user1 = new Domain.Entities.User
        {
            Id = user1Id,
            Username = "johndoe",
            Email = "john@nexus.local",
            PasswordHash = "hash",
            FirstName = "John",
            LastName = "Doe",
            Status = UserStatus.Active
        };

        var user2Id = Guid.NewGuid();
        var user2 = new Domain.Entities.User
        {
            Id = user2Id,
            Username = null,
            Email = "van@nexus.local",
            PasswordHash = "hash",
            FirstName = "Van",
            LastName = "Nguyen",
            Status = UserStatus.Active
        };

        dbContext.Users.AddRange(user1, user2);
        await dbContext.SaveChangesAsync();

        var request = new BatchPublicInfoRequest(new List<Guid> { user1Id, user2Id });
        var result = await service.GetBatchPublicInfoAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        var item1 = Assert.Single(result, r => r.UserId == user1Id.ToString());
        Assert.Equal("j***e", item1.MaskedName);
        Assert.False(item1.IsDeleted);

        var item2 = Assert.Single(result, r => r.UserId == user2Id.ToString());
        Assert.Equal("N***n", item2.MaskedName); // "Nguyen Van" -> first 'N', last 'n'
        Assert.False(item2.IsDeleted);
    }

    [Fact]
    public async Task GetBatchPublicInfoAsync_WhenUserIsSoftDeleted_ReturnsMaskedNameWithIsDeletedTrue()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new InternalUserService(dbContext, NullLogger<InternalUserService>.Instance);

        var deletedUserId = Guid.NewGuid();
        var deletedUser = new Domain.Entities.User
        {
            Id = deletedUserId,
            Username = "deleted_bidder",
            Email = "deleted@nexus.local",
            PasswordHash = "hash",
            FirstName = "Deleted",
            LastName = "Bidder",
            Status = UserStatus.Deleted,
            IsDeleted = true
        };
        dbContext.Users.Add(deletedUser);
        await dbContext.SaveChangesAsync();

        var request = new BatchPublicInfoRequest(new List<Guid> { deletedUserId });
        var result = await service.GetBatchPublicInfoAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        var item = Assert.Single(result);
        Assert.Equal(deletedUserId.ToString(), item.UserId);
        Assert.Equal("d***r", item.MaskedName);
        Assert.True(item.IsDeleted);
    }

    [Fact]
    public async Task GetBatchPublicInfoAsync_WhenUserNotFound_OmittedFromResults()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new InternalUserService(dbContext, NullLogger<InternalUserService>.Instance);

        var nonExistentId = Guid.NewGuid();
        var request = new BatchPublicInfoRequest(new List<Guid> { nonExistentId });
        var result = await service.GetBatchPublicInfoAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetBatchPublicInfoAsync_WhenDuplicateIds_DeduplicatesResults()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new InternalUserService(dbContext, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Username = "dupe_user",
            Email = "dupe@nexus.local",
            PasswordHash = "hash",
            FirstName = "Dupe",
            LastName = "User",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var request = new BatchPublicInfoRequest(new List<Guid> { userId, userId });
        var result = await service.GetBatchPublicInfoAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Fact]
    public async Task CheckReputationAsync_WhenActionIsNotSell_ReturnsErrAccountUnverified()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new InternalUserService(dbContext, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        var response = await service.CheckReputationAsync(new CheckReputationRequest("BUY", userId), CancellationToken.None);

        Assert.NotNull(response);
        Assert.False(response.Allowed);
        Assert.False(response.IsApprovedSeller);
        Assert.Equal("ERR_ACCOUNT_UNVERIFIED", response.FailureReason);
    }

    [Fact]
    public async Task TargetRoleIsolation_BuyerPenaltyDoesNotBlockSeller_AndSellerPenaltyDoesNotBlockBuyer()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new InternalUserService(dbContext, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        var user = new Domain.Entities.User
        {
            Id = userId,
            Email = "dual_role@nexus.local",
            PasswordHash = "hash",
            FirstName = "Dual",
            LastName = "Role",
            Status = UserStatus.Active
        };
        dbContext.Users.Add(user);
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Dual Shop",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "01",
            Hotline = "0901234567",
            Status = SellerProfileStatus.Approved
        });
        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 80
        });

        // Add active penalty only targeted at BUYER role
        dbContext.UserPenalties.Add(new UserPenalty
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetRole = TargetRole.Buyer,
            ViolationType = PenaltyViolationType.AuctionNonPayment,
            PenaltyApplied = PenaltyType.Restriction,
            Reason = "Non-payment in auction",
            Status = PenaltyStatus.Active,
            IssuedAt = DateTimeOffset.UtcNow.AddHours(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(3)
        });
        await dbContext.SaveChangesAsync();

        // 1. Verify Bid Eligibility should be BLOCKED because of Buyer penalty
        var bidResponse = await service.VerifyBidEligibilityAsync(new VerifyBidEligibilityRequest(userId), CancellationToken.None);
        Assert.False(bidResponse.Allowed);
        Assert.Equal("ERR_ACCOUNT_LOCKED_TEMPORARY", bidResponse.FailureReason);

        // 2. Check Reputation (Seller) should still be ALLOWED because penalty is Buyer-only
        var sellerResponse = await service.CheckReputationAsync(new CheckReputationRequest("SELL", userId), CancellationToken.None);
        Assert.True(sellerResponse.Allowed);
        Assert.True(sellerResponse.IsApprovedSeller);
        Assert.Null(sellerResponse.FailureReason);
    }
}
