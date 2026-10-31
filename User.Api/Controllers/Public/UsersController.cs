namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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

    /// <summary>Retrieves the public profile of a user by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUserProfileById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST GetProfileById] Loading public profile for UserId: '{UserId}'", id);
        var profile = await _profileService.GetPublicProfileAsync(id, cancellationToken);

        return Ok(profile);
    }
}
