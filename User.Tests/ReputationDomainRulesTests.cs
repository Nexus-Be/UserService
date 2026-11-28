namespace User.Tests;

using System;
using User.Domain.Constants;
using User.Domain.Entities;
using User.Domain.Enums;
using Xunit;

public sealed class ReputationDomainRulesTests
{
    [Theory]
    [InlineData(0, TrustLevel.Untrusted)]
    [InlineData(39, TrustLevel.Untrusted)]
    [InlineData(40, TrustLevel.Probation)]
    [InlineData(49, TrustLevel.Probation)]
    [InlineData(50, TrustLevel.Standard)]
    [InlineData(69, TrustLevel.Standard)]
    [InlineData(70, TrustLevel.Good)]
    [InlineData(89, TrustLevel.Good)]
    [InlineData(90, TrustLevel.Excellent)]
    [InlineData(100, TrustLevel.Excellent)]
    public void CalculateTrustLevel_MapsScoreRangesToExpectedTrustLevel(int score, TrustLevel expectedLevel)
    {
        var level = ReputationRules.CalculateTrustLevel(score);
        Assert.Equal(expectedLevel, level);
    }

    [Theory]
    [InlineData(50, 15, 65)]
    [InlineData(95, 10, 100)]
    [InlineData(5, -10, 0)]
    public void ClampScore_ClampsBetweenZeroAndOneHundred(int current, int delta, int expected)
    {
        Assert.Equal(expected, ReputationRules.ClampScore(current, delta));
    }

    [Theory]
    [InlineData(1, -2)]
    [InlineData(2, -1)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(5, 1)]
    public void GetReviewScoreDelta_MapsRatingStarsToExpectedScoreDelta(int rating, int expectedDelta)
    {
        Assert.Equal(expectedDelta, ReputationRules.GetReviewScoreDelta(rating));
    }

    [Fact]
    public void ApplyScoreChange_IsolatesBuyerAndSellerTracks_PreventingBuyerFarmingFromRecoveringSellerScore()
    {
        var score = new ReputationScore
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CurrentScore = 60,
            TrustLevel = TrustLevel.Standard
        };

        score.InitializeSellerScore();
        Assert.Equal(50, score.SellerScore);
        Assert.Equal(TrustLevel.Standard, score.SellerTrustLevel);

        // Seller violates rules twice (-5 each -> SellerScore drops from 50 to 40, Probation)
        score.ApplyScoreChange(TargetRole.Seller, -5, isPenalty: true);
        score.ApplyScoreChange(TargetRole.Seller, -5, isPenalty: true);

        Assert.Equal(40, score.SellerScore);
        Assert.Equal(TrustLevel.Probation, score.SellerTrustLevel);
        Assert.Equal(2, score.SellerPenaltyCount);

        // Buyer track remains untouched at 60
        Assert.Equal(60, score.CurrentScore);
        Assert.Equal(TrustLevel.Standard, score.TrustLevel);
        Assert.Equal(0, score.PenaltyCount);

        // Now user buys cheap items repeatedly as a Buyer (+1 order completed, +1 positive review)
        score.ApplyScoreChange(TargetRole.Buyer, +1);
        score.ApplyScoreChange(TargetRole.Buyer, +1, isPositiveReview: true);

        // BuyerScore increases to 62, but SellerScore MUST remain at 40 (still blocked from creating auctions!)
        Assert.Equal(62, score.CurrentScore);
        Assert.Equal(1, score.TotalPositiveReviews);
        Assert.Equal(40, score.SellerScore);
        Assert.Equal(TrustLevel.Probation, score.SellerTrustLevel);
        Assert.Equal(0, score.SellerPositiveReviews);
    }
}
