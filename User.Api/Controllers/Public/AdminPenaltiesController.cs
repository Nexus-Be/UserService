namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Api.Filters;
using User.Application.Common.Models;
using User.Application.Services.Admin;
using User.Domain.Enums;

/// <summary>
/// Quản lý danh mục án phạt vi phạm và thu hồi án phạt dành cho Admin và Support Staff.
/// </summary>
[ApiController]
[Route("api/v1/admin/penalties")]
[RequireRole("admin", "staff")]
public sealed class AdminPenaltiesController : ControllerBase
{
    private readonly IAdminPenaltyService _penaltyService;
    private readonly ILogger<AdminPenaltiesController> _logger;

    public AdminPenaltiesController(
        IAdminPenaltyService penaltyService,
        ILogger<AdminPenaltiesController> logger)
    {
        _penaltyService = penaltyService ?? throw new ArgumentNullException(nameof(penaltyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// [Admin / Staff] Tra cứu danh sách các biên bản xử phạt vi phạm có phân trang và bộ lọc linh hoạt.
    /// </summary>
    /// <param name="page">Số trang hiện tại (bắt đầu từ 1).</param>
    /// <param name="limit">Số bản ghi mỗi trang (mặc định 20).</param>
    /// <param name="userId">Lọc theo ID người dùng chịu phạt.</param>
    /// <param name="targetRole">Lọc theo vai trò mục tiêu: Buyer, Seller, Account.</param>
    /// <param name="status">Lọc theo trạng thái án phạt: Active, Expired, Revoked.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Danh sách biên bản xử phạt kèm siêu dữ liệu phân trang.</returns>
    /// <response code="200">Trả về danh sách án phạt thành công.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedData<AdminPenaltyListItemDto>))]
    public async Task<IActionResult> GetPenalties(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] Guid? userId = null,
        [FromQuery] TargetRole? targetRole = null,
        [FromQuery] PenaltyStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[REST Admin GetPenalties] page={Page}, limit={Limit}, userId={UserId}, targetRole={Role}, status={Status}",
            page, limit, userId, targetRole, status);

        var result = await _penaltyService.GetPenaltiesAsync(page, limit, userId, targetRole, status, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Admin / Staff] Tra cứu chi tiết một biên bản xử phạt cụ thể bao gồm chứng cứ vi phạm và lý do thu hồi (nếu có).
    /// </summary>
    /// <param name="penaltyId">Định danh GUID của biên bản xử phạt.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ xử phạt chi tiết: Loại vi phạm, hình thức phạt, số điểm bị trừ, thời hạn và người ban hành.</returns>
    /// <response code="200">Tìm thấy và trả về chi tiết biên bản xử phạt.</response>
    /// <response code="404">PENALTY_NOT_FOUND nếu không tìm thấy bản ghi.</response>
    [HttpGet("{penaltyId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminPenaltyDetailDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPenaltyDetail(
        [FromRoute] Guid penaltyId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[REST Admin GetPenaltyDetail] penaltyId={PenaltyId}", penaltyId);
        var result = await _penaltyService.GetPenaltyDetailAsync(penaltyId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Thu hồi hoặc hủy bỏ một án phạt đang có hiệu lực (chỉ dành cho Administrator).
    /// </summary>
    /// <param name="penaltyId">Định danh GUID của biên bản xử phạt cần thu hồi.</param>
    /// <param name="dto">Payload chứa lý do thu hồi án phạt.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Kết quả xác nhận thu hồi án phạt và trạng thái mới.</returns>
    /// <response code="200">Thu hồi án phạt thành công và khôi phục quyền cho người dùng.</response>
    /// <response code="400">VAL_INVALID_INPUT nếu thiếu lý do hoặc án phạt đã hết hạn/đã thu hồi.</response>
    /// <response code="404">PENALTY_NOT_FOUND nếu không tìm thấy biên bản xử phạt.</response>
    [HttpPut("{penaltyId:guid}/revoke")]
    [RequireRole("admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminPenaltyRevokedResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokePenalty(
        [FromRoute] Guid penaltyId,
        [FromBody] RevokePenaltyDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var adminId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin RevokePenalty] penaltyId={PenaltyId}, adminId={AdminId}", penaltyId, adminId);
        var result = await _penaltyService.RevokePenaltyAsync(penaltyId, dto, adminId, cancellationToken);
        return Ok(result);
    }
}
