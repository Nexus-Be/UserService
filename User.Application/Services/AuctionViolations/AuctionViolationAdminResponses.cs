namespace User.Application.Services.AuctionViolations;

using System;
using User.Application.Common.Models;
using User.Domain.Enums;

public sealed record AdminAuctionRestrictionDto
{
    public bool IsRestricted { get; init; }
    public AuctionRestrictionType? Type { get; init; }
    public DateTimeOffset? Until { get; init; }
    public string? Reason { get; init; }
}

public sealed record AdminAuctionViolationItemDto
{
    public Guid ViolationId { get; init; }
    public AuctionViolationType? ViolationType { get; init; }
    public Guid? AuctionId { get; init; }
    public Guid? OrderId { get; init; }
    public int StreakAfter { get; init; }
    public int ReputationDelta { get; init; }
    public decimal DepositForfeitedAmount { get; init; }
    public AuctionSanctionType Sanction { get; init; }
    public DateTimeOffset? SanctionUntil { get; init; }
    public string? Reason { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public AuctionViolationSource Source { get; init; }
}

public sealed record AdminAuctionViolationsResponse
{
    public Guid UserId { get; init; }
    public int CurrentStreak { get; init; }
    public AdminAuctionRestrictionDto AuctionRestriction { get; init; } = new();
    public PagedData<AdminAuctionViolationItemDto> Violations { get; init; } =
        PagedData<AdminAuctionViolationItemDto>.From(Array.Empty<AdminAuctionViolationItemDto>(), 1, 20, 0);
}

public sealed record AdjustAuctionViolationSnapshotDto
{
    public int Streak { get; init; }
    public AuctionRestrictionType? Restriction { get; init; }
    public DateTimeOffset? RestrictionUntil { get; init; }
}

public sealed record AdjustAuctionViolationResponse
{
    public Guid UserId { get; init; }
    public AdjustAuctionViolationSnapshotDto Before { get; init; } = new();
    public AdjustAuctionViolationSnapshotDto After { get; init; } = new();
    public Guid AuditId { get; init; }
}
