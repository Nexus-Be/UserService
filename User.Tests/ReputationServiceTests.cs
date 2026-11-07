namespace User.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Services.Reputation;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class ReputationServiceTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"reputation_service_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetMyReputationAsync_ReturnsIsolatedBuyerAndSellerTracks_WithAccurateCanBidAndCanSell()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ReputationService(dbContext, NullLogger<ReputationService>.Instance);

        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "dual@nexus.local",
            PasswordHash = "hash",
            FirstName = "Dual",
            LastName = "User",
            Status = UserStatus.Active
        });

        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ShopName = "Dual Shop",
            PickupAddress = "123 Street",
            ProvinceId = 1,
            DistrictId = 1,
            WardCode = "W1",
            Hotline = "0900000000",
            Status = SellerProfileStatus.Approved
        });

        // Buyer score is 65 (can bid >= 40), Seller score dropped to 45 (cannot sell < 50)
        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 65,
            TrustLevel = TrustLevel.Standard,
            TotalPositiveReviews = 15,
            TotalNegativeReviews = 0,
            PenaltyCount = 0,
            SellerScore = 45,
            SellerTrustLevel = TrustLevel.Probation,
            SellerPositiveReviews = 2,
            SellerNegativeReviews = 3,
            SellerPenaltyCount = 1
        });
        await dbContext.SaveChangesAsync();

        var result = await service.GetMyReputationAsync(userId);

        Assert.Equal(userId, result.UserId);
        Assert.Equal(65, result.CurrentScore);
        Assert.Equal("Standard", result.TrustLevel);
        Assert.True(result.CanBid);
        Assert.Equal(70, result.NextLevelThreshold);

        Assert.Equal(45, result.SellerScore);
        Assert.Equal("Probation", result.SellerTrustLevel);
        Assert.False(result.CanSell);
        Assert.Equal(50, result.SellerNextLevelThreshold);
    }

    [Fact]
    public async Task AdjustReputationAsync_WhenTargetingSellerTrack_UpdatesOnlySellerScoreAndCreatesAuditAndReputationLog()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ReputationService(dbContext, NullLogger<ReputationService>.Instance);

        var adminId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "seller@nexus.local",
            PasswordHash = "hash",
            FirstName = "Seller",
            LastName = "Target",
            Status = UserStatus.Active
        });

        dbContext.ReputationScores.Add(new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentScore = 50,
            TrustLevel = TrustLevel.Standard,
            SellerScore = 50,
            SellerTrustLevel = TrustLevel.Standard
        });
        await dbContext.SaveChangesAsync();

        var response = await service.AdjustReputationAsync(
            userId,
            new AdjustReputationRequest
            {
                TargetRole = TargetRole.Seller,
                ScoreDelta = -15,
                Reason = "Confirmed seller cancellation of active auction with bids."
            },
            adminId);

        Assert.Equal(50, response.PreviousScore);
        Assert.Equal(35, response.NewScore);
        Assert.Equal(-15, response.AppliedDelta);
        Assert.Equal("Untrusted", response.NewTrustLevel);
        Assert.Equal("Seller", response.TargetRole);

        var scoreInDb = await dbContext.ReputationScores.FirstAsync(r => r.UserId == userId);
        Assert.Equal(50, scoreInDb.CurrentScore); // Buyer score untouched!
        Assert.Equal(35, scoreInDb.SellerScore);  // Only Seller score changed!

        var repLog = await dbContext.ReputationLogs.FirstOrDefaultAsync(l => l.UserId == userId);
        Assert.NotNull(repLog);
        Assert.Equal(TargetRole.Seller, repLog.TargetRole);
        Assert.Equal(-15, repLog.ChangeAmount);
        Assert.Equal(35, repLog.ScoreAfter);
        Assert.Equal(ReputationChangeReason.ManualAdjustment, repLog.Reason);

        var auditLog = await dbContext.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == userId.ToString());
        Assert.NotNull(auditLog);
        Assert.Equal(AuditAction.ReputationAdjust, auditLog.Action);
    }

    [Fact]
    public async Task GetUserReputationHistoryAsync_WhenRequesterIsNeitherOwnerNorStaff_ThrowsForbiddenDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ReputationService(dbContext, NullLogger<ReputationService>.Instance);

        var targetUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.GetUserReputationHistoryAsync(targetUserId, otherUserId, isStaffOrAdmin: false));

        Assert.Equal("FORBIDDEN", ex.ErrorCode);
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task GetReputationPolicyAsync_ReturnsDynamicThresholdsAndTrustTiers()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ReputationService(dbContext, NullLogger<ReputationService>.Instance);

        var policy = await service.GetReputationPolicyAsync();

        Assert.Equal(50, policy.InitialScore);
        Assert.Equal(40, policy.MinScoreToBid);
        Assert.Equal(50, policy.MinScoreToSell);
        Assert.Equal(7, policy.AntiFarmingCooldownDays);
        Assert.Equal(5, policy.TrustLevels.Count);
    }
}
