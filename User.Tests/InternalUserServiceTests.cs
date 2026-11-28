namespace User.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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

    private static IMemoryCache CreateMemoryCache() => new MemoryCache(new MemoryCacheOptions());

    [Fact]
    public async Task CheckReputation_And_VerifyBidEligibility_EnforceDualScoreIsolation()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "violating.seller@nexus.local",
            PasswordHash = "hash",
            FirstName = "Violating",
            LastName = "Seller",
            Status = UserStatus.Active
        });

        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Violating Shop",
            PickupAddress = "123 St",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "W1",
            Hotline = "0900000000",
            Status = SellerProfileStatus.Approved
        });

        // Seller violated rules -> SellerScore dropped to 42 (< 50 required to create auction)
        // Even though user farmed BuyerScore up to 85, CheckReputationAsync MUST block auction creation!
        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 85,
            TrustLevel = TrustLevel.Good,
            SellerScore = 42,
            SellerTrustLevel = TrustLevel.Probation
        });
        await dbContext.SaveChangesAsync();

        // INT-04: Seller Create Auction Check -> Uses SellerScore (42 < 50) -> Blocked!
        var sellerCheck = await service.CheckReputationAsync(new CheckReputationRequest
        {
            UserId = userId,
            RequiredAction = "CREATE_AUCTION"
        });

        Assert.False(sellerCheck.IsEligible);
        Assert.Equal(42, sellerCheck.CurrentScore);
        Assert.Equal("REPUTATION_TOO_LOW", sellerCheck.FailureReason);

        // INT-01: Buyer Bid Eligibility Check -> Uses CurrentScore/BuyerScore (85 >= 40) -> Allowed!
        var buyerCheck = await service.VerifyBidEligibilityAsync(new VerifyBidEligibilityRequest
        {
            UserId = userId,
            AuctionId = Guid.NewGuid()
        });

        Assert.True(buyerCheck.IsEligible);
        Assert.Equal(85, buyerCheck.CurrentScore);
        Assert.Null(buyerCheck.FailureReason);
    }

    [Fact]
    public async Task ApplyInternalPenaltyAsync_IsIdempotent_AndTargetsCorrectTrack()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var buyerId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = buyerId,
            Email = "defaulting.buyer@nexus.local",
            PasswordHash = "hash",
            FirstName = "Default",
            LastName = "Buyer",
            Status = UserStatus.Active
        });

        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = buyerId,
            CurrentScore = 50,
            TrustLevel = TrustLevel.Standard,
            SellerScore = 60,
            SellerTrustLevel = TrustLevel.Standard
        });
        await dbContext.SaveChangesAsync();

        var request = new InternalPenaltyRequest
        {
            UserId = buyerId,
            TargetRole = TargetRole.Buyer,
            ViolationType = PenaltyViolationType.PaymentTimeout,
            ReferenceType = ReferenceType.Auction,
            ReferenceId = "AUC-TIMEOUT-01",
            Reason = "Buyer failed to pay winning auction invoice within 24 hours."
        };

        // First call -> deducts 10 from BuyerScore (50 -> 40)
        var first = await service.ApplyInternalPenaltyAsync(request);
        Assert.False(first.IsDuplicate);
        Assert.Equal(50, first.PreviousScore);
        Assert.Equal(40, first.NewScore);
        Assert.Equal(-10, first.AppliedDelta);

        // Second call with identical reference -> Idempotent: no further deduction!
        var second = await service.ApplyInternalPenaltyAsync(request);
        Assert.True(second.IsDuplicate);
        Assert.Equal(40, second.NewScore);

        var scoreInDb = await dbContext.ReputationScores.FirstAsync(r => r.UserId == buyerId);
        Assert.Equal(40, scoreInDb.CurrentScore);
        Assert.Equal(60, scoreInDb.SellerScore); // SellerScore untouched!
    }

    [Fact]
    public async Task ApplyInternalReputationAdjustAsync_EnforcesIdempotencyAndSevenDayAntiFarmingCooldown()
    {
        using var dbContext = CreateInMemoryDbContext();
        using var cache = CreateMemoryCache();
        var service = new InternalUserService(dbContext, cache, NullLogger<InternalUserService>.Instance);

        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();

        dbContext.Users.Add(new User
        {
            Id = sellerId,
            Email = "seller@nexus.local",
            PasswordHash = "hash",
            FirstName = "Seller",
            LastName = "One",
            Status = UserStatus.Active
        });

        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = sellerId,
            CurrentScore = 50,
            SellerScore = 50
        });
        await dbContext.SaveChangesAsync();

        // 1. Order 1 completed with buyerId -> SellerScore +1 (50 -> 51)
        var order1 = await service.ApplyInternalReputationAdjustAsync(new InternalReputationAdjustRequest
        {
            UserId = sellerId,
            TargetRole = TargetRole.Seller,
            PartnerUserId = buyerId,
            ScoreDelta = 1,
            Reason = ReputationChangeReason.OrderCompleted,
            ReferenceType = ReferenceType.Order,
            ReferenceId = "ORD-101"
        });

        Assert.False(order1.IsDuplicate);
        Assert.False(order1.CooldownSuppressed);
        Assert.Equal(1, order1.AppliedDelta);
        Assert.Equal(51, order1.NewScore);

        // 2. Order 2 completed with SAME buyerId within 7 days -> Anti-Farming suppresses +1!
        var order2 = await service.ApplyInternalReputationAdjustAsync(new InternalReputationAdjustRequest
        {
            UserId = sellerId,
            TargetRole = TargetRole.Seller,
            PartnerUserId = buyerId,
            ScoreDelta = 1,
            Reason = ReputationChangeReason.OrderCompleted,
            ReferenceType = ReferenceType.Order,
            ReferenceId = "ORD-102"
        });

        Assert.False(order2.IsDuplicate);
        Assert.True(order2.CooldownSuppressed);
        Assert.Equal(0, order2.AppliedDelta);
        Assert.Equal(51, order2.NewScore);
    }

    [Fact]
    public async Task CheckReputationAsync_WhenSellerProfileIsSuspendedOrPending_ReturnsNotEligible()
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
            FirstName = "Pending",
            LastName = "Seller",
            Status = UserStatus.Active
        });
        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Pending Shop",
            PickupAddress = "123 St",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "W1",
            Hotline = "0900000000",
            Status = SellerProfileStatus.Pending
        });
        dbContext.ReputationScores.Add(ReputationScore.CreateDefault(userId));
        await dbContext.SaveChangesAsync();

        var check = await service.CheckReputationAsync(new CheckReputationRequest
        {
            UserId = userId,
            RequiredAction = "CREATE_AUCTION"
        });

        Assert.False(check.IsEligible);
        Assert.Equal("SELLER_NOT_APPROVED", check.FailureReason);
    }
}
