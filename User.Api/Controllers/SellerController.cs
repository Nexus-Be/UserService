namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Application.Services.Seller;

[ApiController]
[Route("api/v1/seller")]
[Route("api/v1/sellers")]
public sealed class SellerController : ControllerBase
{
    private readonly ISellerService _sellerService;
    private readonly ILogger<SellerController> _logger;

    public SellerController(ISellerService sellerService, ILogger<SellerController> logger)
    {
        _sellerService = sellerService;
        _logger = logger;
    }

    /// <summary>
    /// SELLER-01: Gửi đơn đăng ký mở Gian hàng Shop (Seller Onboarding).
    /// </summary>
    [HttpPost("registration")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(SellerRegistrationResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterSeller(
        [FromBody] CreateSellerRegistrationDto request,
        CancellationToken cancellationToken)
    {
        var userIdHeader = Request.Headers["x-user-id"].ToString();
        if (!Guid.TryParse(userIdHeader, out var userId))
        {
            _logger.LogWarning("[REST RegisterSeller] Missing or invalid x-user-id header.");
            return BadRequest(new
            {
                title = "Invalid user identifier",
                detail = "Missing or invalid x-user-id header.",
                errorCode = "VAL_INVALID_USER_ID"
            });
        }

        _logger.LogInformation("[REST RegisterSeller] Submitting seller onboarding for UserId: '{UserId}', Shop: '{ShopName}'", userId, request?.ShopName);

        var result = await _sellerService.RegisterSellerAsync(userId, request!, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// SELLER-02 / US-SELLER.01: Xem hồ sơ Shop của chính mình (Own Seller Profile).
    /// </summary>
    [HttpGet("profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SellerOwnProfileResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSellerOwnProfile(CancellationToken cancellationToken)
    {
        var userIdHeader = Request.Headers["x-user-id"].ToString();
        if (!Guid.TryParse(userIdHeader, out var userId))
        {
            _logger.LogWarning("[REST GetSellerOwnProfile] Missing or invalid x-user-id header.");
            return BadRequest(new
            {
                title = "Invalid user identifier",
                detail = "Missing or invalid x-user-id header.",
                errorCode = "VAL_INVALID_USER_ID"
            });
        }

        _logger.LogInformation("[REST GetSellerOwnProfile] Fetching seller profile for UserId: '{UserId}'", userId);
        var result = await _sellerService.GetSellerOwnProfileAsync(userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// SELLER-03 / US-SELLER.02: Cập nhật thông tin gian hàng & kho hàng GHN của chính mình.
    /// </summary>
    [HttpPatch("profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SellerOwnProfileUpdatedResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSellerOwnProfile(
        [FromBody] UpdateSellerOwnProfileDto request,
        CancellationToken cancellationToken)
    {
        var userIdHeader = Request.Headers["x-user-id"].ToString();
        if (!Guid.TryParse(userIdHeader, out var userId))
        {
            _logger.LogWarning("[REST UpdateSellerOwnProfile] Missing or invalid x-user-id header.");
            return BadRequest(new
            {
                title = "Invalid user identifier",
                detail = "Missing or invalid x-user-id header.",
                errorCode = "VAL_INVALID_USER_ID"
            });
        }

        _logger.LogInformation("[REST UpdateSellerOwnProfile] Updating seller profile for UserId: '{UserId}'", userId);
        var result = await _sellerService.UpdateSellerOwnProfileAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// ADMIN-07: Xem danh sách hàng đợi các đơn đăng ký mở Shop đang chờ thẩm định.
    /// </summary>
    [HttpGet("/api/v1/admin/seller-registrations")]
    [HttpGet("/api/v1/staff/seller-profiles")]
    [HttpGet("/api/v1/staff/seller-registrations")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResultDto<AdminSellerRegistrationItemDto>))]
    public async Task<IActionResult> ListSellerRegistrations(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[REST ListSellerRegistrations] Fetching page {Page}, limit {Limit}, status {Status}", page, limit, status);
        var result = await _sellerService.GetSellerRegistrationsAsync(page, limit, status, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// ADMIN-08 / US-STAFF.03: Phê duyệt hồ sơ mở Shop và tự động cấp Role SELLER cho tài khoản.
    /// </summary>
    [HttpPut("/api/v1/admin/seller-registrations/{id:guid}/approve")]
    [HttpPut("/api/v1/staff/seller-profiles/{id:guid}/approve")]
    [HttpPut("/api/v1/staff/seller-registrations/{id:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApproveSellerRegistrationResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveSellerRegistration(
        [FromRoute] Guid id,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] ApproveSellerRegistrationDto? body,
        CancellationToken cancellationToken)
    {
        Guid? reviewerId = null;
        var userIdHeader = Request.Headers["x-user-id"].ToString();
        if (Guid.TryParse(userIdHeader, out var parsedReviewerId))
        {
            reviewerId = parsedReviewerId;
        }

        _logger.LogInformation("[REST ApproveSellerRegistration] Approving registration '{Id}' by Staff '{StaffId}'", id, reviewerId);
        var result = await _sellerService.ApproveSellerRegistrationAsync(id, reviewerId, body, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// ADMIN-09 / US-STAFF.03: Từ chối hồ sơ mở Shop kèm lý do bắt buộc.
    /// </summary>
    [HttpPut("/api/v1/admin/seller-registrations/{id:guid}/reject")]
    [HttpPut("/api/v1/staff/seller-profiles/{id:guid}/reject")]
    [HttpPut("/api/v1/staff/seller-registrations/{id:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RejectSellerRegistrationResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectSellerRegistration(
        [FromRoute] Guid id,
        [FromBody] RejectSellerRegistrationDto body,
        CancellationToken cancellationToken)
    {
        Guid? reviewerId = null;
        var userIdHeader = Request.Headers["x-user-id"].ToString();
        if (Guid.TryParse(userIdHeader, out var parsedReviewerId))
        {
            reviewerId = parsedReviewerId;
        }

        _logger.LogInformation("[REST RejectSellerRegistration] Rejecting registration '{Id}' by Staff '{StaffId}'", id, reviewerId);
        var result = await _sellerService.RejectSellerRegistrationAsync(id, reviewerId, body, cancellationToken);
        return Ok(result);
    }
}
