namespace User.Application.Services.Review;

using System;
using System.Collections.Generic;

public sealed record CreateReviewResponse
{
    public Guid ReviewId { get; init; }
    public Guid ReviewerId { get; init; }
    public Guid RevieweeId { get; init; }
    public string ReviewerRole { get; init; } = string.Empty;
    public string TargetRole { get; init; } = string.Empty;
    public string ReferenceType { get; init; } = string.Empty;
    public string ReferenceId { get; init; } = string.Empty;
    public int Rating { get; init; }
    public string? Comment { get; init; }
    public IReadOnlyList<string> MediaUrls { get; init; } = Array.Empty<string>();
    public int ScoreImpact { get; init; }
    public int RevieweeNewScore { get; init; }
    public string RevieweeNewTrustLevel { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record HideReviewResponse
{
    public Guid ReviewId { get; init; }
    public Guid RevieweeId { get; init; }
    public bool IsDisabled { get; init; }
    public string TargetRole { get; init; } = string.Empty;
    public int CompensatedScoreDelta { get; init; }
    public int RevieweeNewScore { get; init; }
    public string RevieweeNewTrustLevel { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public Guid HiddenBy { get; init; }
    public DateTimeOffset HiddenAt { get; init; }
}
