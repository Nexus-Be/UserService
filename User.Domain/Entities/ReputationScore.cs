using User.Domain.Base;
using User.Domain.Constants;
using User.Domain.Enums;

namespace User.Domain.Entities;

public class ReputationScore : BaseEntity<Guid>
{
    public required Guid UserId { get; set; }

    // Buyer Track (100% backward-compatible column names)
    public int CurrentScore { get; set; } = ReputationRules.DefaultInitialScore;
    public TrustLevel TrustLevel { get; set; } = TrustLevel.Standard;
    public int TotalPositiveReviews { get; set; }
    public int TotalNegativeReviews { get; set; }
    public int PenaltyCount { get; set; }

    // Seller Track (Isolated score so Buyer purchases cannot recover violated Seller privileges)
    public int SellerScore { get; set; } = ReputationRules.DefaultInitialScore;
    public TrustLevel SellerTrustLevel { get; set; } = TrustLevel.Standard;
    public int SellerPositiveReviews { get; set; }
    public int SellerNegativeReviews { get; set; }
    public int SellerPenaltyCount { get; set; }

    // Navigation
    public User? User { get; set; }

    public static ReputationScore CreateDefault(Guid userId, Guid? id = null)
    {
        return new ReputationScore
        {
            Id = id ?? Guid.NewGuid(),
            UserId = userId,
            CurrentScore = ReputationRules.DefaultInitialScore,
            TrustLevel = TrustLevel.Standard,
            SellerScore = ReputationRules.DefaultInitialScore,
            SellerTrustLevel = TrustLevel.Standard
        };
    }

    public void InitializeSellerScore()
    {
        SellerScore = ReputationRules.DefaultInitialScore;
        SellerTrustLevel = TrustLevel.Standard;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public (int OldScore, int NewScore, int AppliedDelta) ApplyScoreChange(
        TargetRole targetRole,
        int delta,
        bool isPositiveReview = false,
        bool isNegativeReview = false,
        bool isPenalty = false)
    {
        if (targetRole == TargetRole.Seller)
        {
            var oldScore = SellerScore;
            var newScore = ReputationRules.ClampScore(oldScore, delta);
            SellerScore = newScore;
            SellerTrustLevel = ReputationRules.CalculateTrustLevel(newScore);

            if (isPositiveReview) SellerPositiveReviews = Math.Max(0, SellerPositiveReviews + 1);
            if (isNegativeReview) SellerNegativeReviews = Math.Max(0, SellerNegativeReviews + 1);
            if (isPenalty) SellerPenaltyCount = Math.Max(0, SellerPenaltyCount + 1);

            UpdatedAt = DateTimeOffset.UtcNow;
            return (oldScore, newScore, newScore - oldScore);
        }
        else
        {
            var oldScore = CurrentScore;
            var newScore = ReputationRules.ClampScore(oldScore, delta);
            CurrentScore = newScore;
            TrustLevel = ReputationRules.CalculateTrustLevel(newScore);

            if (isPositiveReview) TotalPositiveReviews = Math.Max(0, TotalPositiveReviews + 1);
            if (isNegativeReview) TotalNegativeReviews = Math.Max(0, TotalNegativeReviews + 1);
            if (isPenalty) PenaltyCount = Math.Max(0, PenaltyCount + 1);

            UpdatedAt = DateTimeOffset.UtcNow;
            return (oldScore, newScore, newScore - oldScore);
        }
    }
}
