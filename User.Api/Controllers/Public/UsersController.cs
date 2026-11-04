namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Application.Services.Admin;
using User.Application.Services.Profile;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IProfileService profileService, ILogger<UsersController> logger)
    {
        _profileService = profileService;
        _logger = logger;
    }

    /// <summary>[Public] Retrieves the safe public profile of a user by ID.</summary>
    [HttpGet("{userId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetUserPublicProfile([FromRoute] Guid userId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST GetUserPublicProfile] Loading public profile for UserId: '{UserId}'", userId);
        var profile = await _profileService.GetPublicProfileAsync(userId, cancellationToken);

        return Ok(profile);
    }

    /// <summary>[Internal / Authorized] Evaluates whether user has active sanctions preventing an action (Buy, Sell, Login).</summary>
    [HttpGet("{userId:guid}/permission-check")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserActionPermissionCheckResponseDto))]
    public async Task<IActionResult> CheckUserActionPermission(
        [FromRoute] Guid userId,
        [FromQuery] string action,
        [FromServices] IAdminPenaltyService penaltyService,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST CheckUserActionPermission] Checking permission for UserId: '{UserId}', Action: '{Action}'", userId, action);
        var result = await penaltyService.CheckUserActionPermissionAsync(userId, action, cancellationToken);
        return Ok(result);
    }

    /// <summary>Backward-compatible wrapper for existing test invocations.</summary>
    [NonAction]
    public Task<IActionResult> GetUserProfileById(Guid id, CancellationToken cancellationToken)
        => GetUserPublicProfile(id, cancellationToken);
}
