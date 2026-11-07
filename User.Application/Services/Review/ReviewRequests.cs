namespace User.Application.Services.Review;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using User.Domain.Enums;

public sealed record CreateReviewRequest
{
    [Required(ErrorMessage = "Reviewee ID is required.")]
    public Guid RevieweeId { get; init; }

    public ReviewerRole ReviewerRole { get; init; } = ReviewerRole.Buyer;

    public ReferenceType ReferenceType { get; init; } = ReferenceType.Order;

    [Required(ErrorMessage = "Reference ID is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Reference ID must be between 1 and 100 characters long.")]
    public string ReferenceId { get; init; } = string.Empty;

    [Required(ErrorMessage = "Rating is required.")]
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
    public int Rating { get; init; }

    [StringLength(1000, ErrorMessage = "Review comment must not exceed 1000 characters long.")]
    public string? Comment { get; init; }

    public List<string>? MediaUrls { get; init; }
}

public sealed record HideReviewRequest
{
    [Required(ErrorMessage = "Reason for hiding the review is required.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Reason must be between 10 and 500 characters long.")]
    public string Reason { get; init; } = string.Empty;
}
