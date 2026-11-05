namespace User.Api.Controllers.Internal;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Application.Services.Admin;

/// <summary>
/// Controller nội bộ phục vụ giao tiếp liên service (Internal Inter-Service Communication).
/// Cung cấp endpoint cho Gateway và các service khác (Order, Catalog) kiểm tra quyền thao tác thời gian thực.
/// </summary>
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

    /// <summary>
    /// Kiểm tra quyền hạn thực hiện hành vi của người dùng dựa trên các án phạt đang hiệu lực.
    /// </summary>
    /// <param name="userId">ID định danh người dùng cần kiểm tra.</param>
    /// <param name="action">Hành vi mục tiêu (ví dụ: Buy, Sell, Login, Comment).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Đối tượng UserActionPermissionCheckResponseDto chứa IsAllowed, Reason, ActiveSanctions.</returns>
    /// <response code="200">Kiểm tra hoàn tất, trả về kết quả cho phép hoặc bị cấm kèm lý do.</response>
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
