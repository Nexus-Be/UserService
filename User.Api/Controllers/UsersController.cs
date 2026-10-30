namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Application.Services.Profile;

[ApiController]
[Route("api/v1/user")]
public sealed class UsersController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IProfileService profileService, ILogger<UsersController> logger)
    {
        _profileService = profileService;
        _logger = logger;
    }

    [HttpGet("/api/v1/me")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserProfileDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUserProfile(CancellationToken cancellationToken)
    {
        var userId = this.GetUserId();
        if (userId == null)
        {
            return BadRequest(new { title = "Invalid user identifier", detail = "Missing or invalid x-user-id header.", errorCode = "VAL_INVALID_USER_ID" });
        }

        _logger.LogInformation("[REST GetProfile] Loading profile for UserId: '{UserId}'", userId);
        var profile = await _profileService.GetProfileAsync(userId.Value, cancellationToken);

        return Ok(profile);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PublicUserProfileDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserProfileById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST GetProfileById] Loading public profile for UserId: '{UserId}'", id);
        var profile = await _profileService.GetPublicProfileAsync(id, cancellationToken);

        return Ok(profile);
    }

    [HttpPatch("/api/v1/me")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserProfileDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCurrentUserProfile([FromBody] UpdateProfileApiRequest request, CancellationToken cancellationToken)
    {
        var userId = this.GetUserId();
        if (userId == null)
        {
            return BadRequest(new { title = "Invalid user identifier", detail = "Missing or invalid x-user-id header.", errorCode = "VAL_INVALID_USER_ID" });
        }

        _logger.LogInformation("[REST UpdateProfile] Updating profile for UserId: '{UserId}'", userId);
        var updatedProfile = await _profileService.UpdateProfileAsync(userId.Value, request, cancellationToken);

        return Ok(updatedProfile);
    }
}
