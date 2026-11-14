namespace User.Application.Services.Internal;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using User.Domain.Enums;

public sealed record CheckReputationRequest
{
    [Required(ErrorMessage = "User ID is required.")]
    public Guid UserId { get; init; }

    public string? Action { get; init; } = "SELL";

    public string? RequiredAction { get; init; }

    public string EffectiveAction => !string.IsNullOrWhiteSpace(Action) ? Action : (RequiredAction ?? "SELL");
}

public sealed record VerifyBidEligibilityRequest
{
    [Required(ErrorMessage = "User ID is required.")]
    public Guid UserId { get; init; }

    public Guid? AuctionId { get; init; }
}

public sealed record BatchPublicInfoRequest
{
    [Required(ErrorMessage = "User IDs list is required.")]
    [MinLength(1, ErrorMessage = "User IDs list must contain at least 1 identifier.")]
    [MaxLength(100, ErrorMessage = "User IDs list cannot exceed 100 identifiers per batch request.")]
    public IReadOnlyList<Guid> UserIds { get; init; } = Array.Empty<Guid>();
}

public sealed record InternalPenaltyRequest
{
    [Required(ErrorMessage = "User ID is required.")]
    public Guid UserId { get; init; }

    public TargetRole TargetRole { get; init; } = TargetRole.Buyer;
    public PenaltyViolationType ViolationType { get; init; } = PenaltyViolationType.PaymentTimeout;
    public ReferenceType ReferenceType { get; init; } = ReferenceType.Auction;

    [Required(ErrorMessage = "Reference ID is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Reference ID must be between 1 and 100 characters long.")]
    public string ReferenceId { get; init; } = string.Empty;

    [Required(ErrorMessage = "Penalty reason is required.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Penalty reason must be between 5 and 500 characters long.")]
    public string Reason { get; init; } = string.Empty;

    public int? CustomScoreDeduction { get; init; }
    public string? PolicyClauseCode { get; init; }
}

public sealed record InternalReputationAdjustRequest
{
    [Required(ErrorMessage = "User ID is required.")]
    public Guid UserId { get; init; }

    public TargetRole TargetRole { get; init; } = TargetRole.Buyer;
    public Guid? PartnerUserId { get; init; }

    [Range(-50, 50, ErrorMessage = "Score delta must be between -50 and 50.")]
    public int ScoreDelta { get; init; } = 1;

    public ReputationChangeReason Reason { get; init; } = ReputationChangeReason.OrderCompleted;
    public ReferenceType ReferenceType { get; init; } = ReferenceType.Order;

    [Required(ErrorMessage = "Reference ID is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Reference ID must be between 1 and 100 characters long.")]
    public string ReferenceId { get; init; } = string.Empty;

    public string? Note { get; init; }
}

public sealed record RecordAuctionViolationRequest
{
    [Required(ErrorMessage = "Violation type is required.")]
    public string ViolationType { get; init; } = string.Empty;

    [Required(ErrorMessage = "Auction ID is required.")]
    public Guid? AuctionId { get; init; }

    [Required(ErrorMessage = "Order ID is required.")]
    public Guid? OrderId { get; init; }

    [Required(ErrorMessage = "Occurred at timestamp is required.")]
    public DateTimeOffset? OccurredAt { get; init; }

    [Range(0, double.MaxValue, ErrorMessage = "Deposit forfeited amount must be non-negative.")]
    public decimal DepositForfeitedAmount { get; init; }
}
