namespace User.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User.Application.Services.Review;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;
using User.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// A partner review moves a reputation score, so it is accepted only for an order that Commerce confirms: the reference must exist,
/// have finished within 30 days, and the caller must be one of its two parties reviewing the other.
/// </summary>
public sealed class ReviewVerificationTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed record World(
        AppDbContext Db, ReviewService Service, FakeCommerceOrderClient Commerce, Clock Clock, Guid BuyerId, Guid SellerId)
    {
        public Guid Stranger { get; } = Guid.NewGuid();
    }

    private static async Task<World> CreateWorld()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"review_verification_{Guid.NewGuid():N}").Options;
        var db = new AppDbContext(options);
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var strangerUser = Guid.NewGuid();
        db.Users.AddRange(
            NewUser(buyerId, "buyer"), NewUser(sellerId, "seller"), NewUser(strangerUser, "stranger"));
        db.SellerProfiles.Add(new SellerProfile
        {
            Id = Guid.NewGuid(), UserId = sellerId, ShopName = "Shop", PickupAddress = "1 Street", ProvinceId = 1, DistrictId = 1,
            WardCode = "W1", Hotline = "0900000000", Status = SellerProfileStatus.Approved
        });
        db.ReputationScores.AddRange(
            new ReputationScore { Id = Guid.NewGuid(), UserId = sellerId, CurrentScore = 50, SellerScore = 50 },
            new ReputationScore { Id = Guid.NewGuid(), UserId = buyerId, CurrentScore = 50, SellerScore = 50 });
        await db.SaveChangesAsync();

        var commerce = new FakeCommerceOrderClient();
        var clock = new Clock(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
        var service = new ReviewService(db, NullLogger<ReviewService>.Instance, commerceClient: commerce, timeProvider: clock);
        return new World(db, service, commerce, clock, buyerId, sellerId);
    }

    private static User NewUser(Guid id, string name) => new()
    {
        Id = id, Email = $"{name}-{id:N}@nexus.local", PasswordHash = "h", FirstName = name, LastName = "Test", Status = UserStatus.Active
    };

    private static CreateReviewRequest Review(
        Guid reviewee, string referenceId, ReferenceType type = ReferenceType.Order,
        ReviewerRole role = ReviewerRole.Buyer, int rating = 5) => new()
    {
        RevieweeId = reviewee, ReviewerRole = role, ReferenceType = type, ReferenceId = referenceId, Rating = rating, Comment = "ok"
    };

    private static async Task<DomainException> Refused(Func<Task> action) => await Assert.ThrowsAsync<DomainException>(action);

    // --- refusals ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task AMadeUpReference_IsRefused_AndNothingIsWritten()
    {
        var w = await CreateWorld();

        var notGuid = await Refused(() => w.Service.CreateReviewAsync(w.BuyerId, Review(w.SellerId, "ORD-MADE-UP")));
        var unknown = await Refused(() => w.Service.CreateReviewAsync(w.BuyerId, Review(w.SellerId, Guid.NewGuid().ToString())));

        Assert.Equal("VAL_INVALID_INPUT", notGuid.ErrorCode);
        Assert.Equal("REVIEW_ORDER_NOT_FOUND", unknown.ErrorCode);
        Assert.Equal(404, unknown.StatusCode);
        Assert.Empty(w.Db.UserReviews);
        Assert.Empty(w.Db.ReputationLogs);
    }

    [Theory]
    [InlineData(ReferenceType.Dispute)]
    [InlineData(ReferenceType.System)]
    public async Task OnlyOrdersAndAuctionOrders_CanBeReviewed(ReferenceType type)
    {
        var w = await CreateWorld();

        var exception = await Refused(() => w.Service.CreateReviewAsync(w.BuyerId, Review(w.SellerId, Guid.NewGuid().ToString(), type)));

        Assert.Equal("REVIEW_REFERENCE_TYPE_NOT_SUPPORTED", exception.ErrorCode);
        Assert.Equal(0, w.Commerce.Calls);
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Confirmed")]
    [InlineData("Shipped")]
    [InlineData("Delivered")]
    [InlineData("Cancelled")]
    public async Task AnUnfinishedOrder_CannotBeReviewed(string status)
    {
        var w = await CreateWorld();
        var id = Guid.NewGuid();
        w.Commerce.Add("Order", id, w.BuyerId, w.SellerId, status, null);

        var exception = await Refused(() => w.Service.CreateReviewAsync(w.BuyerId, Review(w.SellerId, id.ToString())));

        Assert.Equal("REVIEW_ORDER_NOT_COMPLETED", exception.ErrorCode);
        Assert.Equal(409, exception.StatusCode);
        Assert.Empty(w.Db.UserReviews);
    }

    [Fact]
    public async Task APersonOutsideTheOrder_CannotReviewIt_AndTheScoreDoesNotMove()
    {
        var w = await CreateWorld();
        var id = w.Commerce.AddCompleted(w.BuyerId, w.SellerId);

        var exception = await Refused(() => w.Service.CreateReviewAsync(w.Stranger, Review(w.SellerId, id, rating: 1)));

        Assert.Equal("REVIEW_NOT_PARTICIPANT", exception.ErrorCode);
        Assert.Equal(403, exception.StatusCode);
        Assert.Equal(50, (await w.Db.ReputationScores.SingleAsync(r => r.UserId == w.SellerId)).SellerScore);
        Assert.Empty(w.Db.UserReviews);
    }

    [Fact]
    public async Task TheReviewedUser_MustBeTheOtherPartyOfTheOrder()
    {
        var w = await CreateWorld();
        var id = w.Commerce.AddCompleted(w.BuyerId, w.SellerId);

        var exception = await Refused(() => w.Service.CreateReviewAsync(w.BuyerId, Review(w.Stranger, id)));

        Assert.Equal("REVIEW_TARGET_MISMATCH", exception.ErrorCode);
        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task AfterThirtyDays_TheReviewWindowIsClosed()
    {
        var w = await CreateWorld();
        var finished = w.Clock.Now.AddDays(-30).AddMinutes(-1);
        var late = w.Commerce.AddCompleted(w.BuyerId, w.SellerId, completedAt: finished);
        var inTime = w.Commerce.AddCompleted(w.BuyerId, w.SellerId, completedAt: w.Clock.Now.AddDays(-29));

        var expired = await Refused(() => w.Service.CreateReviewAsync(w.BuyerId, Review(w.SellerId, late)));
        var created = await w.Service.CreateReviewAsync(w.BuyerId, Review(w.SellerId, inTime));

        Assert.Equal("REVIEW_PERIOD_EXPIRED", expired.ErrorCode);
        Assert.Equal(409, expired.StatusCode);
        Assert.Equal(inTime, created.ReferenceId);
    }

    [Fact]
    public async Task WhenCommerceCannotAnswer_NothingIsAcceptedWithoutProof()
    {
        var w = await CreateWorld();
        var id = w.Commerce.AddCompleted(w.BuyerId, w.SellerId);
        w.Commerce.Unavailable = true;

        var exception = await Refused(() => w.Service.CreateReviewAsync(w.BuyerId, Review(w.SellerId, id)));

        Assert.Equal("REVIEW_VERIFICATION_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(503, exception.StatusCode);
        Assert.Empty(w.Db.UserReviews);
    }

    [Fact]
    public async Task WithoutACommerceClient_TheServiceFailsClosed()
    {
        var w = await CreateWorld();
        var service = new ReviewService(w.Db, NullLogger<ReviewService>.Instance);

        var exception = await Refused(() => service.CreateReviewAsync(w.BuyerId, Review(w.SellerId, Guid.NewGuid().ToString())));

        Assert.Equal("REVIEW_VERIFICATION_UNAVAILABLE", exception.ErrorCode);
    }

    [Fact]
    public async Task TheSameOrder_CannotBeReviewedTwice()
    {
        var w = await CreateWorld();
        var id = w.Commerce.AddCompleted(w.BuyerId, w.SellerId);
        await w.Service.CreateReviewAsync(w.BuyerId, Review(w.SellerId, id));

        var exception = await Refused(() => w.Service.CreateReviewAsync(w.BuyerId, Review(w.SellerId, id)));

        Assert.Equal("REVIEW_ALREADY_EXISTS", exception.ErrorCode);
        Assert.Equal(1, await w.Db.UserReviews.CountAsync());
    }

    // --- what is accepted --------------------------------------------------------------------------------------------

    [Fact]
    public async Task TheBuyer_ReviewsTheSeller_AndTheSellerReviewsTheBuyer_OfTheSameOrder()
    {
        var w = await CreateWorld();
        var id = w.Commerce.AddCompleted(w.BuyerId, w.SellerId);

        var byBuyer = await w.Service.CreateReviewAsync(w.BuyerId, Review(w.SellerId, id));
        var bySeller = await w.Service.CreateReviewAsync(w.SellerId, Review(w.BuyerId, id, role: ReviewerRole.Seller));

        Assert.Equal("Seller", byBuyer.TargetRole);
        Assert.Equal("Buyer", bySeller.TargetRole);
        Assert.Equal(2, await w.Db.UserReviews.CountAsync());
    }

    [Fact]
    public async Task TheRole_ComesFromTheOrder_NotFromTheRequest()
    {
        var w = await CreateWorld();
        var id = w.Commerce.AddCompleted(w.BuyerId, w.SellerId);

        // The seller claims to be reviewing as a buyer; the order says they are the seller.
        var response = await w.Service.CreateReviewAsync(w.SellerId, Review(w.BuyerId, id, role: ReviewerRole.Buyer));

        Assert.Equal("Seller", response.ReviewerRole);
        Assert.Equal("Buyer", response.TargetRole);
        Assert.Equal(ReviewerRole.Seller, (await w.Db.UserReviews.SingleAsync()).ReviewerRole);
    }

    [Fact]
    public async Task AnAuctionOrder_CanBeReviewed()
    {
        var w = await CreateWorld();
        var id = w.Commerce.AddCompleted(w.BuyerId, w.SellerId, "Auction");

        var response = await w.Service.CreateReviewAsync(w.BuyerId, Review(w.SellerId, id, ReferenceType.Auction));

        Assert.Equal("Auction", response.ReferenceType);
        Assert.Equal(id, response.ReferenceId);
    }

    [Fact]
    public async Task AnAuctionOrderId_IsNotASubOrder()
    {
        var w = await CreateWorld();
        var id = w.Commerce.AddCompleted(w.BuyerId, w.SellerId, "Auction");

        var exception = await Refused(() => w.Service.CreateReviewAsync(w.BuyerId, Review(w.SellerId, id, ReferenceType.Order)));

        Assert.Equal("REVIEW_ORDER_NOT_FOUND", exception.ErrorCode);
    }

    [Fact]
    public async Task TwoOrdersOfTheSamePair_AreBothReviewed_ButOnlyTheFirstPositiveReviewAddsPoints()
    {
        var w = await CreateWorld();
        var first = w.Commerce.AddCompleted(w.BuyerId, w.SellerId);
        var second = w.Commerce.AddCompleted(w.BuyerId, w.SellerId);
        var third = w.Commerce.AddCompleted(w.BuyerId, w.SellerId);

        var one = await w.Service.CreateReviewAsync(w.BuyerId, Review(w.SellerId, first));
        var two = await w.Service.CreateReviewAsync(w.BuyerId, Review(w.SellerId, second));
        var bad = await w.Service.CreateReviewAsync(w.BuyerId, Review(w.SellerId, third, rating: 1));

        Assert.True(one.ScoreImpact > 0);
        Assert.Equal(0, two.ScoreImpact);                       // the cooldown: stored, but no extra points
        Assert.True(bad.ScoreImpact < 0);                       // a bad review is never held back
        Assert.Equal(3, await w.Db.UserReviews.CountAsync());
    }

    // --- eligibility -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Eligibility_ForACompletedOrder_NamesTheOtherPartyAndTheWindow()
    {
        var w = await CreateWorld();
        var finished = w.Clock.Now.AddDays(-2);
        var id = w.Commerce.AddCompleted(w.BuyerId, w.SellerId, completedAt: finished);

        var buyer = await w.Service.GetEligibilityAsync(w.BuyerId, ReferenceType.Order, id);
        var seller = await w.Service.GetEligibilityAsync(w.SellerId, ReferenceType.Order, id);

        Assert.True(buyer.CanReview);
        Assert.Null(buyer.Reason);
        Assert.Equal(w.SellerId, buyer.RevieweeId);
        Assert.Equal("Buyer", buyer.ReviewerRole);
        Assert.Equal(finished.AddDays(30), buyer.ExpiresAt);
        Assert.Equal(w.BuyerId, seller.RevieweeId);
        Assert.Equal("Seller", seller.ReviewerRole);
    }

    [Fact]
    public async Task Eligibility_AfterReviewing_SaysAlreadyReviewed()
    {
        var w = await CreateWorld();
        var id = w.Commerce.AddCompleted(w.BuyerId, w.SellerId);
        await w.Service.CreateReviewAsync(w.BuyerId, Review(w.SellerId, id));

        var buyer = await w.Service.GetEligibilityAsync(w.BuyerId, ReferenceType.Order, id);
        var seller = await w.Service.GetEligibilityAsync(w.SellerId, ReferenceType.Order, id);

        Assert.False(buyer.CanReview);
        Assert.True(buyer.AlreadyReviewed);
        Assert.Equal("ALREADY_REVIEWED", buyer.Reason);
        Assert.True(seller.CanReview);                          // the other party can still review
    }

    [Theory]
    [InlineData("Delivered", -1, "ORDER_NOT_COMPLETED")]
    [InlineData("Completed", -31, "PERIOD_EXPIRED")]
    public async Task Eligibility_ExplainsWhyNot(string status, int completedDaysAgo, string reason)
    {
        var w = await CreateWorld();
        var id = Guid.NewGuid();
        w.Commerce.Add("Order", id, w.BuyerId, w.SellerId, status, status == "Completed" ? w.Clock.Now.AddDays(completedDaysAgo) : null);

        var result = await w.Service.GetEligibilityAsync(w.BuyerId, ReferenceType.Order, id.ToString());

        Assert.False(result.CanReview);
        Assert.Equal(reason, result.Reason);
    }

    [Fact]
    public async Task Eligibility_ToAStranger_RevealsNothingAboutTheOrder()
    {
        var w = await CreateWorld();
        var id = w.Commerce.AddCompleted(w.BuyerId, w.SellerId);

        var stranger = await w.Service.GetEligibilityAsync(w.Stranger, ReferenceType.Order, id);
        var missing = await w.Service.GetEligibilityAsync(w.Stranger, ReferenceType.Order, Guid.NewGuid().ToString());

        Assert.False(stranger.CanReview);
        Assert.Equal("NOT_PARTICIPANT", stranger.Reason);
        Assert.Null(stranger.RevieweeId);
        Assert.Null(stranger.ReviewerRole);
        Assert.Null(stranger.ExpiresAt);
        Assert.Equal("ORDER_NOT_FOUND", missing.Reason);
    }

    [Fact]
    public async Task Eligibility_WhenCommerceIsDown_IsA503_NotAGuess()
    {
        var w = await CreateWorld();
        w.Commerce.Unavailable = true;

        var exception = await Refused(() => w.Service.GetEligibilityAsync(w.BuyerId, ReferenceType.Order, Guid.NewGuid().ToString()));

        Assert.Equal("REVIEW_VERIFICATION_UNAVAILABLE", exception.ErrorCode);
    }
}
