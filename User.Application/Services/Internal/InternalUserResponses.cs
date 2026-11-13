namespace User.Application.Services.Internal;

using System;
using System.Collections.Generic;
using User.Domain.Enums;

public sealed record CheckReputationResponse
{
    public Guid UserId { get; init; }
    public bool Allowed { get; init; }
    public bool IsEligible { get => Allowed; init => Allowed = value; }
    public bool IsApprovedSeller { get; init; }
    public string? UserStatus { get; init; }
    public int CurrentScore { get; init; }
    public string TrustLevel { get; init; } = string.Empty;
    public string? FailureReason { get; init; }
}

public sealed record VerifyBidEligibilityResponse
{
    public Guid UserId { get; init; }
    public bool Allowed { get; init; }
    public bool IsEligible { get => Allowed; init => Allowed = value; }
    public int CurrentScore { get; init; }
    public string? FailureReason { get; init; }
}

public sealed record UserPublicInfoItem
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string MaskedDisplayName { get; init; } = string.Empty;
    public string MaskedName { get; init; } = string.Empty;
    public bool IsDeleted { get; init; }
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

public sealed record AuctionRestrictionDto
{
    public bool IsRestricted { get; init; }
    public AuctionRestrictionType? Type { get; init; }
    public DateTimeOffset? Until { get; init; }
    public string? Reason { get; init; }
}

public sealed record AuctionDepositProfileResponse
{
    public Guid UserId { get; init; }
    public UserStatus AccountStatus { get; init; }
    public int? ReputationScore { get; init; }
    public bool IsNewUser { get; init; }
    public int ViolationStreak { get; init; }
    public AuctionRestrictionDto AuctionRestriction { get; init; } = new();
}

public sealed record AuctionSanctionDto
{
    public AuctionSanctionType Type { get; init; }
    public DateTimeOffset? Until { get; init; }
}

public sealed record RecordAuctionViolationResponse
{
    public Guid ViolationId { get; init; }
    public bool Created { get; init; }
    public int PreviousStreak { get; init; }
    public int NewStreak { get; init; }
    public int ReputationBefore { get; init; }
    public int ReputationAfter { get; init; }
    public AuctionSanctionDto Sanction { get; init; } = new();
}
