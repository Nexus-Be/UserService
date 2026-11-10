namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using User.Api.Controllers;
using User.Api.Controllers.Internal;
using User.Application.Services.Admin;
using User.Application.Services.Internal;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class AuctionInterserviceEndpointsTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"auction_interservice_tests_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static IMemoryCache CreateMemoryCache() => new MemoryCache(new MemoryCacheOptions());

    // -------------------------------------------------------------------------
    // 1. CheckReputation Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CheckReputation_WhenEligibleApprovedSeller_ReturnsAllowedTrueAndNullFailureReason()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "seller.good@nexus.local",
            PasswordHash = "hash",
            FirstName = "Nguyen",
            LastName = "Van A",
            Status = UserStatus.Active
        });

        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Nguyen Shop",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "W1",
            Hotline = "0900000000",
            Status = SellerProfileStatus.Approved
        });

        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 70,
            SellerScore = 80,
            SellerTrustLevel = TrustLevel.Good
        });

        await dbContext.SaveChangesAsync();

        var request = new CheckReputationRequest
        {
            Action = "SELL",
            UserId = userId
        };

        var response = await service.CheckReputationAsync(request);

        Assert.Equal(userId, response.UserId);
        Assert.True(response.Allowed);
        Assert.True(response.IsApprovedSeller);
        Assert.Equal("Active", response.UserStatus);
        Assert.Null(response.FailureReason);
    }

    [Fact]
    public async Task CheckReputation_WhenSellerProfilePending_ReturnsAllowedFalseAndSellerNotApproved()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "pending.seller@nexus.local",
            PasswordHash = "hash",
            FirstName = "Tran",
            LastName = "Van B",
            Status = UserStatus.Active
        });

        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Pending Shop",
            PickupAddress = "456 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "W2",
            Hotline = "0901111111",
            Status = SellerProfileStatus.Pending
        });

        await dbContext.SaveChangesAsync();

        var request = new CheckReputationRequest { Action = "SELL", UserId = userId };
        var response = await service.CheckReputationAsync(request);

        Assert.Equal(userId, response.UserId);
        Assert.False(response.Allowed);
        Assert.False(response.IsApprovedSeller);
        Assert.Equal("SELLER_NOT_APPROVED", response.FailureReason);
    }

    [Fact]
    public async Task CheckReputation_WhenUserBanned_ReturnsAllowedFalseAndAccountBanned()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "banned.user@nexus.local",
            PasswordHash = "hash",
            FirstName = "Banned",
            LastName = "User",
            Status = UserStatus.Banned
        });
        await dbContext.SaveChangesAsync();

        var response = await service.CheckReputationAsync(new CheckReputationRequest { UserId = userId });

        Assert.Equal(userId, response.UserId);
        Assert.False(response.Allowed);
        Assert.False(response.IsApprovedSeller);
        Assert.Equal("Banned", response.UserStatus);
        Assert.Equal("ACCOUNT_BANNED", response.FailureReason);
    }

    [Fact]
    public async Task CheckReputation_WhenUserNotFound_ReturnsAllowedFalseAndUserNotFound()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var nonExistentId = Guid.NewGuid();
        var response = await service.CheckReputationAsync(new CheckReputationRequest { UserId = nonExistentId });

        Assert.Equal(nonExistentId, response.UserId);
        Assert.False(response.Allowed);
        Assert.False(response.IsApprovedSeller);
        Assert.Equal("NotFound", response.UserStatus);
        Assert.Equal("USER_NOT_FOUND", response.FailureReason);
    }

    // -------------------------------------------------------------------------
    // 2. VerifyBidEligibility Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task VerifyBidEligibility_WhenEligibleBuyer_ReturnsAllowedTrue()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "good.buyer@nexus.local",
            PasswordHash = "hash",
            FirstName = "Le",
            LastName = "Van C",
            Status = UserStatus.Active
        });

        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 75,
            SellerScore = 50
        });
        await dbContext.SaveChangesAsync();

        // Auction sends only UserId (no AuctionId)
        var response = await service.VerifyBidEligibilityAsync(new VerifyBidEligibilityRequest { UserId = userId });

        Assert.Equal(userId, response.UserId);
        Assert.True(response.Allowed);
        Assert.Null(response.FailureReason);
    }

    [Fact]
    public async Task VerifyBidEligibility_WhenBuyerHasActiveRestriction_ReturnsAllowedFalse()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "restricted.buyer@nexus.local",
            PasswordHash = "hash",
            FirstName = "Pham",
            LastName = "Van D",
            Status = UserStatus.Active
        });

        dbContext.UserPenalties.Add(new UserPenalty
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetRole = TargetRole.Buyer,
            ViolationType = PenaltyViolationType.PaymentTimeout,
            PenaltyApplied = PenaltyType.Restriction,
            Reason = "Non-payment violation",
            Status = PenaltyStatus.Active,
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var response = await service.VerifyBidEligibilityAsync(new VerifyBidEligibilityRequest { UserId = userId });

        Assert.Equal(userId, response.UserId);
        Assert.False(response.Allowed);
        Assert.Equal("ACCOUNT_RESTRICTED", response.FailureReason);
    }

    // -------------------------------------------------------------------------
    // 3. BatchPublicInfo Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetBatchPublicInfo_MasksNamesCorrectly_AndSetsIsDeletedFlag()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var activeId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();

        dbContext.Users.Add(new User
        {
            Id = activeId,
            Email = "active@nexus.local",
            PasswordHash = "hash",
            FirstName = "Nguyen",
            LastName = "Vinh",
            Status = UserStatus.Active
        });

        dbContext.Users.Add(new User
        {
            Id = deletedId,
            Email = "deleted@nexus.local",
            PasswordHash = "hash",
            FirstName = "John",
            LastName = "Doe",
            Status = UserStatus.Deleted,
            IsDeleted = true
        });

        await dbContext.SaveChangesAsync();

        var response = await service.GetBatchPublicInfoAsync(new BatchPublicInfoRequest
        {
            UserIds = new[] { activeId, deletedId }
        });

        Assert.Equal(2, response.Users.Count);

        var activeItem = response.Users.First(u => u.UserId == activeId);
        Assert.False(activeItem.IsDeleted);
        Assert.Equal("N***h", activeItem.MaskedName);

        var deletedItem = response.Users.First(u => u.UserId == deletedId);
        Assert.True(deletedItem.IsDeleted);
        Assert.Equal("J***e", deletedItem.MaskedName);
    }

    [Fact]
    public async Task GetBatchPublicInfo_WhenExceeds100Ids_ThrowsDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var userIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToList();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.GetBatchPublicInfoAsync(new BatchPublicInfoRequest { UserIds = userIds }));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
        Assert.Equal(400, ex.StatusCode);
    }

    // -------------------------------------------------------------------------
    // 4. Controller Endpoints & { data: ... } Envelope Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InternalUsersController_Endpoints_ReturnDataEnvelope()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "controller.test@nexus.local",
            PasswordHash = "hash",
            FirstName = "Minh",
            LastName = "An",
            Status = UserStatus.Active
        });
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Minh An Shop",
            PickupAddress = "789 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "W3",
            Hotline = "0902222222",
            Status = SellerProfileStatus.Approved
        });
        await dbContext.SaveChangesAsync();

        var controller = new InternalUsersController(
            penaltyService: null,
            logger: NullLogger<InternalUsersController>.Instance,
            internalUserService: service);

        // 1. CheckReputation
        var repActionResult = await controller.CheckReputation(new CheckReputationRequest { UserId = userId, Action = "SELL" });
        var repOk = Assert.IsType<OkObjectResult>(repActionResult.Result);
        var repProp = repOk.Value?.GetType().GetProperty("data")?.GetValue(repOk.Value);
        Assert.NotNull(repProp);
        var repData = Assert.IsType<CheckReputationResponse>(repProp);
        Assert.True(repData.Allowed);
        Assert.True(repData.IsApprovedSeller);

        // 2. VerifyBidEligibility
        var bidActionResult = await controller.VerifyBidEligibility(new VerifyBidEligibilityRequest { UserId = userId });
        var bidOk = Assert.IsType<OkObjectResult>(bidActionResult.Result);
        var bidProp = bidOk.Value?.GetType().GetProperty("data")?.GetValue(bidOk.Value);
        Assert.NotNull(bidProp);
        var bidData = Assert.IsType<VerifyBidEligibilityResponse>(bidProp);
        Assert.True(bidData.Allowed);

        // 3. BatchPublicInfo
        var batchActionResult = await controller.GetBatchPublicInfo(new BatchPublicInfoRequest { UserIds = new[] { userId } });
        var batchOk = Assert.IsType<OkObjectResult>(batchActionResult.Result);
        var batchProp = batchOk.Value?.GetType().GetProperty("data")?.GetValue(batchOk.Value);
        Assert.NotNull(batchProp);
        var batchData = Assert.IsAssignableFrom<IEnumerable<UserPublicInfoItem>>(batchProp);
        Assert.Single(batchData);
    }

    [Fact]
    public async Task UsersController_PublicDualRoute_Endpoints_ReturnDataEnvelope()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "dualroute.test@nexus.local",
            PasswordHash = "hash",
            FirstName = "Hoang",
            LastName = "Nam",
            Status = UserStatus.Active
        });
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Hoang Nam Shop",
            PickupAddress = "321 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "W4",
            Hotline = "0903333333",
            Status = SellerProfileStatus.Approved
        });
        await dbContext.SaveChangesAsync();

        var controller = new UsersController(
            profileService: null!,
            logger: NullLogger<UsersController>.Instance,
            internalUserService: service);

        // 1. CheckReputation on /api/v1/users/check-reputation
        var repActionResult = await controller.CheckReputation(new CheckReputationRequest { UserId = userId, Action = "SELL" });
        var repOk = Assert.IsType<OkObjectResult>(repActionResult);
        var repProp = repOk.Value?.GetType().GetProperty("data")?.GetValue(repOk.Value);
        Assert.NotNull(repProp);
        var repData = Assert.IsType<CheckReputationResponse>(repProp);
        Assert.True(repData.Allowed);
        Assert.True(repData.IsApprovedSeller);

        // 2. VerifyBidEligibility on /api/v1/users/verify-bid-eligibility
        var bidActionResult = await controller.VerifyBidEligibility(new VerifyBidEligibilityRequest { UserId = userId });
        var bidOk = Assert.IsType<OkObjectResult>(bidActionResult);
        var bidProp = bidOk.Value?.GetType().GetProperty("data")?.GetValue(bidOk.Value);
        Assert.NotNull(bidProp);
        var bidData = Assert.IsType<VerifyBidEligibilityResponse>(bidProp);
        Assert.True(bidData.Allowed);

        // 3. BatchPublicInfo on /api/v1/users/batch-public-info
        var batchActionResult = await controller.GetBatchPublicInfo(new BatchPublicInfoRequest { UserIds = new[] { userId } });
        var batchOk = Assert.IsType<OkObjectResult>(batchActionResult);
        var batchProp = batchOk.Value?.GetType().GetProperty("data")?.GetValue(batchOk.Value);
        Assert.NotNull(batchProp);
        var batchData = Assert.IsAssignableFrom<IEnumerable<UserPublicInfoItem>>(batchProp);
        Assert.Single(batchData);
    }
}
