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

/// <summary>
/// Whether the caller may review an order. <c>Reason</c> is one of ALREADY_REVIEWED, ORDER_NOT_COMPLETED, PERIOD_EXPIRED,
/// NOT_PARTICIPANT or ORDER_NOT_FOUND when <c>CanReview</c> is false. <c>RevieweeId</c> and <c>ReviewerRole</c> are given only to a
/// party of the order; <c>ExpiresAt</c> is the end of the review window once the order has finished.
/// </summary>
public sealed record ReviewEligibilityResponse(
    bool CanReview,
    bool AlreadyReviewed,
    string? Reason,
    Guid? RevieweeId,
    string? ReviewerRole,
    DateTimeOffset? ExpiresAt);
