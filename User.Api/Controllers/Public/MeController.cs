namespace User.Api.Controllers;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Api.Filters;
using User.Application.Services.Profile;

[ApiController]
[Route("api/v1/me")]
public sealed class MeController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly ILogger<MeController> _logger;

    public MeController(IProfileService profileService, ILogger<MeController> logger)
    {
        _profileService = profileService;
        _logger = logger;
    }

    /// <summary>Retrieves the authenticated user's own profile.</summary>
    [HttpGet]
    [RequireRole]
    public async Task<IActionResult> GetCurrentUserProfile(CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST GetProfile] Loading profile for UserId: '{UserId}'", userId);
        var profile = await _profileService.GetProfileAsync(userId, cancellationToken);

        return Ok(profile);
    }

    /// <summary>Updates the authenticated user's own profile.</summary>
    [HttpPatch]
    [RequireRole]
    public async Task<IActionResult> UpdateCurrentUserProfile([FromBody] UpdateProfileApiRequest request, CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST UpdateProfile] Updating profile for UserId: '{UserId}'", userId);
        var updatedProfile = await _profileService.UpdateProfileAsync(userId, request, cancellationToken);

        return Ok(updatedProfile);
    }
}
