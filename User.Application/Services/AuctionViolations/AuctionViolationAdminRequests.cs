namespace User.Application.Services.AuctionViolations;

using System;
using System.ComponentModel.DataAnnotations;
using User.Domain.Enums;

public sealed record AdjustAuctionViolationRequest
{
    [Required(ErrorMessage = "Adjustment action is required.")]
    public AuctionAdminAdjustAction? Action { get; init; }

    public int? Value { get; init; }

    public DateTimeOffset? Until { get; init; }

    [Required(ErrorMessage = "Adjustment reason is required.")]
    [StringLength(1000, MinimumLength = 3, ErrorMessage = "Adjustment reason must be between 3 and 1000 characters long.")]
    public string Reason { get; init; } = string.Empty;
}
