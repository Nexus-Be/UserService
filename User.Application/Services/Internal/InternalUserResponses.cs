namespace User.Application.Services.Internal;

using System;
using System.Collections.Generic;

public sealed record CheckReputationResponse
{
    public bool IsEligible { get; init; }
    public int CurrentScore { get; init; }
    public string TrustLevel { get; init; } = string.Empty;
    public string? FailureReason { get; init; }
}

public sealed record VerifyBidEligibilityResponse
{
    public bool IsEligible { get; init; }
    public int CurrentScore { get; init; }
    public string? FailureReason { get; init; }
}

public sealed record UserPublicInfoItem
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string MaskedDisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public int ReputationScore { get; init; }
    public int SellerReputationScore { get; init; }
}

public sealed record BatchPublicInfoResponse
{
    public IReadOnlyList<UserPublicInfoItem> Users { get; init; } = Array.Empty<UserPublicInfoItem>();
}

public sealed record InternalPenaltyResponse
{
    public Guid UserId { get; init; }
    public string TargetRole { get; init; } = string.Empty;
    public bool IsDuplicate { get; init; }
    public Guid? PenaltyId { get; init; }
    public int PreviousScore { get; init; }
    public int NewScore { get; init; }
    public int AppliedDelta { get; init; }
    public string NewTrustLevel { get; init; } = string.Empty;
}

public sealed record InternalReputationAdjustResponse
{
    public Guid UserId { get; init; }
    public string TargetRole { get; init; } = string.Empty;
    public bool IsDuplicate { get; init; }
    public bool CooldownSuppressed { get; init; }
    public int PreviousScore { get; init; }
    public int NewScore { get; init; }
    public int AppliedDelta { get; init; }
    public string NewTrustLevel { get; init; } = string.Empty;
}
