namespace User.Application.Services.Review;

using System;
using System.Collections.Generic;

public sealed record UserReviewItemDto
{
    public Guid ReviewId { get; init; }
    public Guid ReviewerId { get; init; }
    public string ReviewerDisplayNameMasked { get; init; } = string.Empty;
    public string? ReviewerAvatarUrl { get; init; }
    public string ReviewerRole { get; init; } = string.Empty;
    public int Rating { get; init; }
    public string? Comment { get; init; }
    public IReadOnlyList<string> MediaUrls { get; init; } = Array.Empty<string>();
    public string ReferenceType { get; init; } = string.Empty;
    public string ReferenceId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}
