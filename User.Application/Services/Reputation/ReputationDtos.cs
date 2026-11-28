namespace User.Application.Services.Reputation;

using System;

public sealed record ReputationHistoryItemDto
{
    public Guid LogId { get; init; }
    public string TargetRole { get; init; } = string.Empty;
    public int ChangeAmount { get; init; }
    public int ScoreAfter { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? ReferenceType { get; init; }
    public string? ReferenceId { get; init; }
    public string? Note { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record MyPenaltyItemDto
{
    public Guid PenaltyId { get; init; }
    public string TargetRole { get; init; } = string.Empty;
    public string ViolationType { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string PenaltyApplied { get; init; } = string.Empty;
    public int PointsDeducted { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? PolicyClauseCode { get; init; }
    public DateTimeOffset IssuedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public bool IsCurrentlyActive { get; init; }
}

public sealed record TrustLevelTierDto
{
    public string Level { get; init; } = string.Empty;
    public int MinScore { get; init; }
    public int MaxScore { get; init; }
    public string Description { get; init; } = string.Empty;
}
