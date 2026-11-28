namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Filters;
using User.Application.Services.Admin;

/// <summary>
/// Quản trị danh mục các vai trò hệ thống dành riêng cho Administrator.
/// </summary>
[ApiController]
[Route("api/v1/admin/roles")]
[RequireRole("admin")]
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

    /// <summary>
    /// [Admin] Tra cứu toàn bộ danh mục các vai trò khả dụng trong hệ thống phục vụ phân quyền.
    /// </summary>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Danh sách các vai trò hệ thống: Buyer, Seller, SupportStaff, Admin.</returns>
    /// <response code="200">Trả về danh sách vai trò thành công.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminRoleListResponseDto))]
    public async Task<IActionResult> ListRoles(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[REST Admin ListRoles] Incoming request to list all roles");
        var result = await _adminUserService.GetRolesAsync(cancellationToken);
        return Ok(result);
    }
}
