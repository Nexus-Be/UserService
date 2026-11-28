namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Api.Filters;
using User.Application.Services.Review;

/// <summary>
/// Kiểm duyệt đánh giá đối tác dành cho Admin và Support Staff.
/// </summary>
[ApiController]
[Route("api/v1/admin/reviews")]
[RequireRole("admin", "staff")]
public sealed class AdminReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ILogger<AdminReviewsController> _logger;

    public AdminReviewsController(IReviewService reviewService, ILogger<AdminReviewsController> logger)
    {
        _reviewService = reviewService;
        _logger = logger;
    }

    /// <summary>
    /// [Admin / Staff] Ẩn đánh giá vi phạm quy chế và tự động hoàn trả điểm uy tín cho người bị hại.
    /// </summary>
    [HttpPut("{reviewId:guid}/hide")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HideReviewResponse))]
    public async Task<IActionResult> HideReview(
        [FromRoute] Guid reviewId,
        [FromBody] HideReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var actorId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin HideReview] Hiding review '{ReviewId}' by Actor '{ActorId}'", reviewId, actorId);
        var result = await _reviewService.HideReviewAsync(reviewId, request, actorId, cancellationToken);
        return Ok(result);
    }
}
