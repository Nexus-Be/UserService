namespace User.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Services.Review;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

public sealed class ReviewServiceTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"review_service_test_db_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateReviewAsync_EnforcesSevenDayAntiFarmingForPositiveReviews_WhileApplyingNegativeDeductionsImmediately()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ReviewService(dbContext, NullLogger<ReviewService>.Instance);

        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();

        dbContext.Users.AddRange(
            new User
            {
                Id = buyerId,
                Email = "buyer@nexus.local",
                PasswordHash = "hash",
                FirstName = "Vinh",
                LastName = "Tran",
                Status = UserStatus.Active
            },
            new User
            {
                Id = sellerId,
                Email = "seller@nexus.local",
                PasswordHash = "hash",
                FirstName = "Shop",
                LastName = "Owner",
                Status = UserStatus.Active
            });

        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = sellerId,
            ShopName = "Official Store",
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
            UserId = sellerId,
            CurrentScore = 50,
            TrustLevel = TrustLevel.Standard,
            SellerScore = 50,
            SellerTrustLevel = TrustLevel.Standard
        });
        await dbContext.SaveChangesAsync();

        // 1. First 5-star review from Buyer -> Seller gets +1 on SellerScore (50 -> 51)
        var firstReview = await service.CreateReviewAsync(buyerId, new CreateReviewRequest
        {
            RevieweeId = sellerId,
            ReviewerRole = ReviewerRole.Buyer,
            ReferenceType = ReferenceType.Order,
            ReferenceId = "ORD-001",
            Rating = 5,
            Comment = "Fast shipping and authentic item."
        });

        Assert.Equal(1, firstReview.ScoreImpact);
        var sellerScoreAfterFirst = await dbContext.ReputationScores.FirstAsync(r => r.UserId == sellerId);
        Assert.Equal(51, sellerScoreAfterFirst.SellerScore);
        Assert.Equal(50, sellerScoreAfterFirst.CurrentScore); // Buyer score untouched

        // 2. Second 5-star review from SAME Buyer within 7 days on another order -> Anti-Farming triggers: ScoreImpact = 0!
        var secondReview = await service.CreateReviewAsync(buyerId, new CreateReviewRequest
        {
            RevieweeId = sellerId,
            ReviewerRole = ReviewerRole.Buyer,
            ReferenceType = ReferenceType.Order,
            ReferenceId = "ORD-002",
            Rating = 5,
            Comment = "Second order within same week."
        });

        Assert.Equal(0, secondReview.ScoreImpact);
        var sellerScoreAfterSecond = await dbContext.ReputationScores.FirstAsync(r => r.UserId == sellerId);
        Assert.Equal(51, sellerScoreAfterSecond.SellerScore); // Still 51!

        // 3. Negative 1-star review from SAME Buyer within 7 days -> Always applied immediately (-2 -> 51 to 49)!
        var thirdReview = await service.CreateReviewAsync(buyerId, new CreateReviewRequest
        {
            RevieweeId = sellerId,
            ReviewerRole = ReviewerRole.Buyer,
            ReferenceType = ReferenceType.Order,
            ReferenceId = "ORD-003",
            Rating = 1,
            Comment = "Counterfeit item sent on third order."
        });

        Assert.Equal(-2, thirdReview.ScoreImpact);
        var sellerScoreAfterThird = await dbContext.ReputationScores.FirstAsync(r => r.UserId == sellerId);
        Assert.Equal(49, sellerScoreAfterThird.SellerScore);
        Assert.Equal(TrustLevel.Probation, sellerScoreAfterThird.SellerTrustLevel);
    }

    [Fact]
    public async Task HideReviewAsync_ReversesExactScoreImpactOnTargetTrack_AndCreatesDisputeCompensationLog()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ReviewService(dbContext, NullLogger<ReviewService>.Instance);

        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        dbContext.Users.AddRange(
            new User { Id = buyerId, Email = "b@nexus.local", PasswordHash = "h", FirstName = "Buyer", LastName = "One", Status = UserStatus.Active },
            new User { Id = sellerId, Email = "s@nexus.local", PasswordHash = "h", FirstName = "Seller", LastName = "One", Status = UserStatus.Active });

        dbContext.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(),
            UserId = sellerId,
            ShopName = "Shop One",
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
            UserId = sellerId,
            CurrentScore = 50,
            SellerScore = 50
        });
        await dbContext.SaveChangesAsync();

        // Buyer leaves a defamatory 1-star review (-2 to SellerScore -> 48)
        var created = await service.CreateReviewAsync(buyerId, new CreateReviewRequest
        {
            RevieweeId = sellerId,
            ReviewerRole = ReviewerRole.Buyer,
            ReferenceType = ReferenceType.Order,
            ReferenceId = "ORD-DEFAME-1",
            Rating = 1,
            Comment = "Malicious competitor review."
        });

        var scoreBeforeHide = await dbContext.ReputationScores.FirstAsync(r => r.UserId == sellerId);
        Assert.Equal(48, scoreBeforeHide.SellerScore);
        Assert.Equal(1, scoreBeforeHide.SellerNegativeReviews);

        // Staff hides the review -> SellerScore is compensated +2 back to 50!
        var hideResult = await service.HideReviewAsync(
            created.ReviewId,
            new HideReviewRequest { Reason = "Verified malicious competitor review without valid evidence." },
            staffId);

        Assert.True(hideResult.IsDisabled);
        Assert.Equal(2, hideResult.CompensatedScoreDelta);
        Assert.Equal(50, hideResult.RevieweeNewScore);

        var scoreAfterHide = await dbContext.ReputationScores.FirstAsync(r => r.UserId == sellerId);
        Assert.Equal(50, scoreAfterHide.SellerScore);
        Assert.Equal(0, scoreAfterHide.SellerNegativeReviews);

        // Hidden review is excluded from public list
        var publicReviews = await service.GetUserReviewsAsync(sellerId, page: 1, pageSize: 20);
        Assert.Empty(publicReviews.Items);
    }

    [Fact]
    public async Task CreateReviewAsync_WhenSelfReviewOrDuplicate_ThrowsExpectedDomainException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new ReviewService(dbContext, NullLogger<ReviewService>.Instance);

        var userId = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateReviewAsync(userId, new CreateReviewRequest
            {
                RevieweeId = userId,
                ReviewerRole = ReviewerRole.Buyer,
                ReferenceType = ReferenceType.Order,
                ReferenceId = "ORD-SELF",
                Rating = 5
            }));

        Assert.Equal("SELF_REVIEW_NOT_ALLOWED", ex.ErrorCode);
    }
}
