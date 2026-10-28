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

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminUserListPagedResultDto))]
    public async Task<IActionResult> ListUsers(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? role = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[REST Admin ListUsers] Page: {Page}, Limit: {Limit}, Keyword: '{Keyword}', Role: '{Role}', Status: '{Status}'",
            page, limit, keyword, role, status);

        var result = await _adminUserService.GetUsersAsync(page, limit, keyword, role, status, cancellationToken);

        return Ok(result);
    }
}
