namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Api.Filters;
using User.Application.Common;
using User.Application.Services.Admin;
using User.Application.Common.Models;

[ApiController]
[Route("api/v1/admin/users")]
[RequireRole("admin", "staff")]
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

    /// <summary>Creates a user account on behalf of an administrator.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(AdminUserCreatedResponseDto))]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateAdminUserDto request,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var adminId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin CreateUser] Incoming request for Email: '{Email}'", request?.Email);

        var result = await _adminUserService.CreateUserAsync(request!, adminId, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Lists user accounts with pagination and filters.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedData<AdminUserListItemDto>))]
    public async Task<IActionResult> ListUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? role = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = PaginationConstants.Normalize(page, pageSize);

        _logger.LogInformation("[REST Admin ListUsers] Page: {Page}, PageSize: {PageSize}, Keyword: '{Keyword}', Role: '{Role}', Status: '{Status}'",
            safePage, safePageSize, keyword, role, status);

        var result = await _adminUserService.GetUsersAsync(safePage, safePageSize, keyword, role, status, cancellationToken);

        return Ok(result);
    }

    /// <summary>Retrieves the administrative detail of a single user.</summary>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminUserDetailDto))]
    public async Task<IActionResult> GetUserDetail(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[REST Admin GetUserDetail] Incoming request for UserId: '{UserId}'", userId);

        var result = await _adminUserService.GetUserDetailAsync(userId, cancellationToken);

        return Ok(result);
    }

    /// <summary>Replaces the assigned roles of a user account.</summary>
    [HttpPut("{userId:guid}/roles")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminUserRolesResponseDto))]
    public async Task<IActionResult> AssignUserRoles(
        [FromRoute] Guid userId,
        [FromBody] AssignAdminUserRolesDto request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var adminId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin AssignRoles] Incoming request for UserId: '{UserId}'", userId);

        var result = await _adminUserService.AssignUserRolesAsync(userId, request, adminId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Updates an existing user account.</summary>
    [HttpPut("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminUserUpdatedResponseDto))]
    public async Task<IActionResult> UpdateUser(
        [FromRoute] Guid userId,
        [FromBody] UpdateAdminUserDto request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var adminId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin UpdateUser] Incoming request for UserId: '{UserId}'", userId);

        var result = await _adminUserService.UpdateUserAsync(userId, request, adminId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Deletes a user account.</summary>
    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteUser(
        [FromRoute] Guid userId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] DeleteAdminUserDto? request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var adminId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin DeleteUser] Incoming request for UserId: '{UserId}'", userId);

        await _adminUserService.DeleteUserAsync(userId, request, adminId, cancellationToken);
        return NoContent();
    }
}
