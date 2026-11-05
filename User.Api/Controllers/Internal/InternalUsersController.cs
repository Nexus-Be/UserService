namespace User.Api.Controllers.Internal;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Application.Services.Admin;

[ApiController]
[Route("api/v1/internal/users")]
public sealed class InternalUsersController : ControllerBase
{
    private readonly IAdminPenaltyService _penaltyService;
    private readonly ILogger<InternalUsersController> _logger;

    public InternalUsersController(
        IAdminPenaltyService penaltyService,
        ILogger<InternalUsersController> logger)
    {
        _penaltyService = penaltyService ?? throw new ArgumentNullException(nameof(penaltyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>[Internal Inter-Service] Evaluates whether user has active sanctions preventing an action (Buy, Sell, Login).</summary>
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
}
