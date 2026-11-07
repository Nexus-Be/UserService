namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Api.Filters;
using User.Application.Common.Models;
using User.Application.Services.Profile;
using User.Application.Services.Report;
using User.Application.Services.Reputation;
using User.Application.Services.Review;
using User.Domain.Enums;

/// <summary>
/// Tra cứu thông tin hồ sơ người dùng công khai, thẻ uy tín, đánh giá và gửi báo cáo vi phạm.
/// </summary>
[ApiController]
[Route("api/v1/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly IReputationService? _reputationService;
    private readonly IReviewService? _reviewService;
    private readonly IReportService? _reportService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IProfileService profileService,
        ILogger<UsersController> logger,
        IReputationService? reputationService = null,
        IReviewService? reviewService = null,
        IReportService? reportService = null)
    {
        _profileService = profileService;
        _logger = logger;
        _reputationService = reputationService;
        _reviewService = reviewService;
        _reportService = reportService;
    }

    /// <summary>
    /// [Public] Tra cứu thông tin hồ sơ công khai an toàn của một người dùng bất kỳ theo ID.
    /// </summary>
    [HttpGet("{userId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PublicUserProfileDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserPublicProfile([FromRoute] Guid userId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST GetUserPublicProfile] Loading public profile for UserId: '{UserId}'", userId);
        var profile = await _profileService.GetPublicProfileAsync(userId, cancellationToken);

        return Ok(profile);
    }

    /// <summary>
    /// [Public] Xem thẻ điểm uy tín công khai của một người dùng.
    /// </summary>
    [HttpGet("{userId:guid}/reputation")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PublicReputationResponse))]
    public async Task<IActionResult> GetUserReputation([FromRoute] Guid userId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST GetUserReputation] Loading public reputation summary for UserId: '{UserId}'", userId);
        var result = await _reputationService!.GetUserReputationSummaryAsync(userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Authenticated] Xem lịch sử biến động điểm uy tín (Chỉ chính chủ hoặc Admin/Staff).
    /// </summary>
    [HttpGet("{userId:guid}/reputation/history")]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedData<ReputationHistoryItemDto>))]
    public async Task<IActionResult> GetUserReputationHistory(
        [FromRoute] Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] TargetRole? targetRole = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        if (!this.TryGetUserId(out var requesterId, out var authError))
        {
            return authError;
        }

        var isStaffOrAdmin = this.IsAdmin() || this.HasRole("staff");
        _logger.LogInformation("[REST GetUserReputationHistory] Loading reputation history for UserId: '{UserId}' by Requester: '{RequesterId}'", userId, requesterId);

        var result = await _reputationService!.GetUserReputationHistoryAsync(
            userId, requesterId, isStaffOrAdmin, page, pageSize, targetRole, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// [Public] Xem danh sách các đánh giá công khai của một người dùng.
    /// </summary>
    [HttpGet("{userId:guid}/reviews")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedData<UserReviewItemDto>))]
    public async Task<IActionResult> GetUserReviews(
        [FromRoute] Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? rating = null,
        [FromQuery] int? filterRating = null,
        [FromQuery] ReviewerRole? reviewerRole = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var effectiveRating = filterRating ?? rating;
        _logger.LogInformation("[REST GetUserReviews] Loading public reviews for UserId: '{UserId}'", userId);
        var result = await _reviewService!.GetUserReviewsAsync(userId, page, pageSize, effectiveRating, reviewerRole, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Authenticated] Gửi báo cáo tố cáo hành vi vi phạm của một người dùng khác.
    /// </summary>
    [HttpPost("reports")]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ReportDetailResponse))]
    public async Task<IActionResult> CreateUserReport(
        [FromBody] CreateReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var reporterId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST CreateUserReport] Submitting report from '{ReporterId}' against '{ReportedUserId}'", reporterId, request.ReportedUserId);
        var result = await _reportService!.CreateReportAsync(reporterId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Hàm bọc tương thích ngược phục vụ các bài kiểm thử tự động cũ.
    /// </summary>
    [NonAction]
    public Task<IActionResult> GetUserProfileById(Guid id, CancellationToken cancellationToken)
        => GetUserPublicProfile(id, cancellationToken);
}
