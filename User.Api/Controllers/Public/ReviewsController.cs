namespace User.Api.Controllers;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Api.Filters;
using User.Application.Services.Review;

/// <summary>
/// Quản lý đánh giá đối tác sau giao dịch (Peer Reviews).
/// </summary>
[ApiController]
[Route("api/v1/reviews")]
public sealed class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(IReviewService reviewService, ILogger<ReviewsController> logger)
    {
        _reviewService = reviewService;
        _logger = logger;
    }

    /// <summary>
    /// [Authenticated] Tạo đánh giá đối tác sau giao dịch và tự động cập nhật điểm uy tín theo vai trò.
    /// </summary>
    [HttpPost]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(CreateReviewResponse))]
    public async Task<IActionResult> CreateReview(
        [FromBody] CreateReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var reviewerId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST CreateReview] User '{ReviewerId}' reviewing '{RevieweeId}'", reviewerId, request.RevieweeId);
        var result = await _reviewService.CreateReviewAsync(reviewerId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
