namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Application.Services.Admin;

[ApiController]
[Route("api/v1/admin/users")]
[Route("api/v1/admin/user")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(
        IAdminUserService adminUserService,
        ILogger<AdminUsersController> logger)
    {
        _adminUserService = adminUserService ?? throw new ArgumentNullException(nameof(adminUserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(AdminUserCreatedResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateAdminUserDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST Admin CreateUser] Incoming request for Email: '{Email}'", request?.Email);

        Guid? adminId = null;
        if (Request.Headers.TryGetValue("x-user-id", out var headerVal) &&
            Guid.TryParse(headerVal.ToString(), out var parsedAdminId))
        {
            adminId = parsedAdminId;
        }

        var result = await _adminUserService.CreateUserAsync(request!, adminId, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }
}
