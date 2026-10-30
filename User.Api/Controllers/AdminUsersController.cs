namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Application.Services.Admin;

[ApiController]
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

        var adminId = this.GetUserId();
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

    [HttpGet("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminUserDetailDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserDetail(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[REST Admin GetUserDetail] Incoming request for UserId: '{UserId}'", userId);

        var result = await _adminUserService.GetUserDetailAsync(userId, cancellationToken);

        return Ok(result);
    }

    [HttpGet("/api/v1/admin/roles")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminRoleListResponseDto))]
    public async Task<IActionResult> ListRoles(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[REST Admin ListRoles] Incoming request to list all roles");
        var result = await _adminUserService.GetRolesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut("{userId:guid}/roles")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminUserRolesResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignUserRoles(
        [FromRoute] Guid userId,
        [FromBody] AssignAdminUserRolesDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[REST Admin AssignRoles] Incoming request for UserId: '{UserId}'", userId);

        var adminId = this.GetUserId();
        var result = await _adminUserService.AssignUserRolesAsync(userId, request, adminId, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminUserUpdatedResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser(
        [FromRoute] Guid userId,
        [FromBody] UpdateAdminUserDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[REST Admin UpdateUser] Incoming request for UserId: '{UserId}'", userId);

        var adminId = this.GetUserId();
        var result = await _adminUserService.UpdateUserAsync(userId, request, adminId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(
        [FromRoute] Guid userId,
        [FromBody] DeleteAdminUserDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[REST Admin DeleteUser] Incoming request for UserId: '{UserId}'", userId);

        var adminId = this.GetUserId();
        await _adminUserService.DeleteUserAsync(userId, request, adminId, cancellationToken);
        return NoContent();
    }
}
