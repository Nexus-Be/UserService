namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Application.Services.Seller;

[ApiController]
[Route("api/v1/seller")]
public sealed class SellerController : ControllerBase
{
    private readonly ISellerService _sellerService;
    private readonly ILogger<SellerController> _logger;

    public SellerController(ISellerService sellerService, ILogger<SellerController> logger)
    {
        _sellerService = sellerService;
        _logger = logger;
    }

    /// <summary>Submits a seller onboarding registration application.</summary>
    [HttpPost("registration")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(SellerRegistrationResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterSeller(
        [FromBody] CreateSellerRegistrationDto request,
        CancellationToken cancellationToken)
    {
        var userId = this.GetUserId();
        if (userId == null)
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

        var result = await _sellerService.RegisterSellerAsync(userId.Value, request!, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Retrieves own seller profile.</summary>
    [HttpGet("profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SellerOwnProfileResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSellerOwnProfile(CancellationToken cancellationToken)
    {
        var userId = this.GetUserId();
        if (userId == null)
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
        var result = await _sellerService.GetSellerOwnProfileAsync(userId.Value, cancellationToken);
        return Ok(result);
    }

    /// <summary>Updates own seller profile and warehouse address.</summary>
    [HttpPatch("profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SellerOwnProfileUpdatedResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSellerOwnProfile(
        [FromBody] UpdateSellerOwnProfileDto request,
        CancellationToken cancellationToken)
    {
        var userId = this.GetUserId();
        if (userId == null)
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
        var result = await _sellerService.UpdateSellerOwnProfileAsync(userId.Value, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Lists pending seller registration applications for review.</summary>
    [HttpGet("/api/v1/admin/seller-registrations")]
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

    /// <summary>Approves a seller registration and assigns seller role.</summary>
    [HttpPut("/api/v1/admin/seller-registrations/{id:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApproveSellerRegistrationResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveSellerRegistration(
        [FromRoute] Guid id,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] ApproveSellerRegistrationDto? body,
        CancellationToken cancellationToken)
    {
        var reviewerId = this.GetUserId();

        _logger.LogInformation("[REST ApproveSellerRegistration] Approving registration '{Id}' by Staff '{StaffId}'", id, reviewerId);
        var result = await _sellerService.ApproveSellerRegistrationAsync(id, reviewerId, body, cancellationToken);
        return Ok(result);
    }

    /// <summary>Rejects a seller registration with a required reason.</summary>
    [HttpPut("/api/v1/admin/seller-registrations/{id:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RejectSellerRegistrationResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectSellerRegistration(
        [FromRoute] Guid id,
        [FromBody] RejectSellerRegistrationDto body,
        CancellationToken cancellationToken)
    {
        var reviewerId = this.GetUserId();

        _logger.LogInformation("[REST RejectSellerRegistration] Rejecting registration '{Id}' by Staff '{StaffId}'", id, reviewerId);
        var result = await _sellerService.RejectSellerRegistrationAsync(id, reviewerId, body, cancellationToken);
        return Ok(result);
    }
}
