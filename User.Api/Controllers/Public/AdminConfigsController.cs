namespace User.Api.Controllers;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Api.Filters;
using User.Application.Common.Models;
using User.Application.Services.AdminConfig;

/// <summary>
/// Quản trị tham số cấu hình hệ thống động dành riêng cho Administrator.
/// </summary>
[ApiController]
[Route("api/v1/admin/configs")]
[RequireRole("admin")]
public sealed class AdminConfigsController : ControllerBase
{
    private readonly IAdminConfigService _configService;
    private readonly ILogger<AdminConfigsController> _logger;

    public AdminConfigsController(IAdminConfigService configService, ILogger<AdminConfigsController> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// [Admin] Xem danh sách tham số cấu hình toàn sàn có phân trang.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedData<SystemConfigItemDto>))]
    public async Task<IActionResult> GetConfigs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        _logger.LogInformation("[REST Admin GetConfigs] Querying configs page={Page}, pageSize={PageSize}", page, pageSize);
        var result = await _configService.GetConfigsAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Cập nhật giá trị một tham số cấu hình hệ thống và làm mới bộ nhớ đệm.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UpdateSystemConfigResponse))]
    public async Task<IActionResult> UpdateConfig(
        [FromBody] UpdateSystemConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var adminId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin UpdateConfig] Updating config '{Key}' by Admin '{AdminId}'", request.Key, adminId);
        var result = await _configService.UpdateConfigAsync(request, adminId, cancellationToken);
        return Ok(result);
    }
}
