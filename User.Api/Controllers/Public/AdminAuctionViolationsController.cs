namespace User.Api.Controllers;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Api.Filters;
using User.Application.Services.AuctionViolations;

/// <summary>Quản lý Streak và lịch sử vi phạm đấu giá dành cho Admin và Support Staff.</summary>
[ApiController]
[Route("api/v1/admin/users/{userId:guid}/auction-violations")]
[RequireRole("admin", "staff")]
public sealed class AdminAuctionViolationsController : ControllerBase
{
    private readonly IAuctionViolationAdminService _auctionViolationAdminService;
    private readonly ILogger<AdminAuctionViolationsController> _logger;

    public AdminAuctionViolationsController(
        IAuctionViolationAdminService auctionViolationAdminService,
        ILogger<AdminAuctionViolationsController> logger)
    {
        _auctionViolationAdminService = auctionViolationAdminService ?? throw new ArgumentNullException(nameof(auctionViolationAdminService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>[Admin / Staff] Tra cứu Streak hiện tại, trạng thái hạn chế đấu giá và lịch sử vi phạm đấu giá có phân trang.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminAuctionViolationsResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuctionViolations(
        [FromRoute] Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[REST Admin GetAuctionViolations] userId={UserId}, page={Page}, pageSize={PageSize}",
            userId,
            page,
            pageSize);

        var result = await _auctionViolationAdminService.GetAuctionViolationsAsync(userId, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>[Admin] Điều chỉnh Streak hoặc trạng thái hạn chế đấu giá của người dùng (bắt buộc lý do và ghi AuditLog).</summary>
    [HttpPost("adjust")]
    [RequireRole("admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdjustAuctionViolationResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdjustAuctionViolation(
        [FromRoute] Guid userId,
        [FromBody] AdjustAuctionViolationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var adminId, out var authError))
        {
            return authError!;
        }

        _logger.LogInformation(
            "[REST Admin AdjustAuctionViolation] userId={UserId}, adminId={AdminId}, action={Action}",
            userId,
            adminId,
            request.Action);

        var ipAddress = HttpContext?.Connection?.RemoteIpAddress?.ToString();
        var userAgent = HttpContext?.Request?.Headers["User-Agent"].ToString();
        var traceId = Activity.Current?.Id ?? HttpContext?.TraceIdentifier;

        var result = await _auctionViolationAdminService.AdjustAuctionViolationAsync(
            userId,
            request,
            adminId,
            ipAddress,
            userAgent,
            traceId,
            cancellationToken);

        return Ok(result);
    }
}
