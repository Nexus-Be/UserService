namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Filters;
using User.Application.Services.Admin;

[ApiController]
[Route("api/v1/admin/roles")]
[RequireRole("admin", "staff")]
public sealed class AdminRolesController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;
    private readonly ILogger<AdminRolesController> _logger;

    public AdminRolesController(
        IAdminUserService adminUserService,
        ILogger<AdminRolesController> logger)
    {
        _adminUserService = adminUserService ?? throw new ArgumentNullException(nameof(adminUserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Lists all user roles available for assignment.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminRoleListResponseDto))]
    public async Task<IActionResult> ListRoles(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[REST Admin ListRoles] Incoming request to list all roles");
        var result = await _adminUserService.GetRolesAsync(cancellationToken);
        return Ok(result);
    }
}
