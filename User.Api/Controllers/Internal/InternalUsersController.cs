namespace User.Api.Controllers.Internal;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Application.Services.Admin;
using User.Application.Services.Internal;

/// <summary>[Internal] Internal inter-service controller for authorization and auction integration.</summary>
[ApiController]
[Route("api/v1/internal/users")]
public sealed class InternalUsersController : ControllerBase
{
    private readonly IAdminPenaltyService _penaltyService;
    private readonly IInternalUserService _internalUserService;
    private readonly ILogger<InternalUsersController> _logger;

    public InternalUsersController(
        IAdminPenaltyService penaltyService,
        IInternalUserService internalUserService,
        ILogger<InternalUsersController> logger)
    {
        _penaltyService = penaltyService ?? throw new ArgumentNullException(nameof(penaltyService));
        _internalUserService = internalUserService ?? throw new ArgumentNullException(nameof(internalUserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>[Internal] Verifies user action permission based on currently active penalties.</summary>
    /// <param name="userId">Target user identifier.</param>
    /// <param name="action">Target action (e.g., Buy, Sell, Login, Comment).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Permission check result with allowed status and active sanctions.</returns>
    [HttpGet("{userId:guid}/permission-check")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserActionPermissionCheckResponseDto))]
    public async Task<IActionResult> CheckUserActionPermission(
        [FromRoute] Guid userId,
        [FromQuery] string action,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Internal CheckUserActionPermission] Checking permission for UserId: '{UserId}', Action: '{Action}'", userId, action);
        var result = await _penaltyService.CheckUserActionPermissionAsync(userId, action, cancellationToken);
        return Ok(result);
    }

    /// <summary>[Internal] Checks reputation and seller qualification when opening an auction session.</summary>
    /// <param name="request">Request payload containing action (SELL) and userId.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Check result containing allowed, isApprovedSeller, userStatus, and failureReason.</returns>
    [HttpPost("check-reputation")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CheckReputationResponse))]
    public async Task<IActionResult> CheckReputation(
        [FromBody] CheckReputationRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Internal CheckReputation] Action: '{Action}', UserId: '{UserId}'", request.Action, request.UserId);
        var result = await _internalUserService.CheckReputationAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>[Internal] Verifies buyer eligibility when placing bids in an auction (SLA under 800ms).</summary>
    /// <param name="request">Request payload containing buyer userId.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Check result containing allowed and failureReason.</returns>
    [HttpPost("verify-bid-eligibility")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VerifyBidEligibilityResponse))]
    public async Task<IActionResult> VerifyBidEligibility(
        [FromBody] VerifyBidEligibilityRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Internal VerifyBidEligibility] UserId: '{UserId}'", request.UserId);
        var result = await _internalUserService.VerifyBidEligibilityAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>[Internal] Retrieves public masked names for auction bid history feed (max 100 users).</summary>
    /// <param name="request">List of user IDs to look up (maximum 100 IDs).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of user public info records containing userId, maskedName, isDeleted.</returns>
    [HttpPost("batch-public-info")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<UserPublicInfoDto>))]
    public async Task<IActionResult> GetBatchPublicInfo(
        [FromBody] BatchPublicInfoRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Internal GetBatchPublicInfo] Count: {Count}", request.UserIds?.Count ?? 0);
        var result = await _internalUserService.GetBatchPublicInfoAsync(request, cancellationToken);
        return Ok(result);
    }
}
