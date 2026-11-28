namespace User.Application.Services.Reputation;

using System;
using System.Collections.Generic;

public sealed record MyReputationResponse
{
    public Guid UserId { get; init; }

    // Buyer Track
    public int CurrentScore { get; init; }
    public string TrustLevel { get; init; } = string.Empty;
    public bool CanBid { get; init; }
    public int TotalPositiveReviews { get; init; }
    public int TotalNegativeReviews { get; init; }
    public int PenaltyCount { get; init; }
    public int? NextLevelThreshold { get; init; }

    // Seller Track
    public int SellerScore { get; init; }
    public string SellerTrustLevel { get; init; } = string.Empty;
    public bool CanSell { get; init; }
    public int SellerPositiveReviews { get; init; }
    public int SellerNegativeReviews { get; init; }
    public int SellerPenaltyCount { get; init; }
    public int? SellerNextLevelThreshold { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record PublicReputationResponse
{
    public Guid UserId { get; init; }
    public int CurrentScore { get; init; }
    public string TrustLevel { get; init; } = string.Empty;
    public int TotalPositiveReviews { get; init; }
    public int TotalNegativeReviews { get; init; }
    public int TotalReviews { get; init; }
    public decimal PositiveRatio { get; init; }
    public int SellerScore { get; init; }
    public string SellerTrustLevel { get; init; } = string.Empty;
    public int SellerPositiveReviews { get; init; }
    public int SellerNegativeReviews { get; init; }
    public IReadOnlyList<string> Badges { get; init; } = Array.Empty<string>();
    public DateTimeOffset MemberSince { get; init; }
}

public sealed record AdjustReputationResponse
{
    public Guid UserId { get; init; }
    public string TargetRole { get; init; } = string.Empty;
    public int PreviousScore { get; init; }
    public int NewScore { get; init; }
    public int AppliedDelta { get; init; }
    public string PreviousTrustLevel { get; init; } = string.Empty;
    public string NewTrustLevel { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public Guid AdjustedBy { get; init; }
    public DateTimeOffset AdjustedAt { get; init; }
}

public sealed record ReputationPolicyResponse
{
    public bool EnforcementEnabled { get; init; }
    public int InitialScore { get; init; }
    public int MinScore { get; init; }
    public int MaxScore { get; init; }
    public int MinScoreToBid { get; init; }
    public int MinScoreToSell { get; init; }
    public int PaymentTimeoutPenalty { get; init; }
    public int SellerCancelPenalty { get; init; }
    public int OrderCompletedReward { get; init; }
    public int AntiFarmingCooldownDays { get; init; }
    public IReadOnlyList<TrustLevelTierDto> TrustLevels { get; init; } = Array.Empty<TrustLevelTierDto>();
}
