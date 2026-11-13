namespace User.Api.Controllers.Internal;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Application.Services.Admin;
using User.Application.Services.Internal;

/// <summary>
/// Controller nội bộ phục vụ giao tiếp liên service (Internal Inter-Service Communication).
/// Cung cấp endpoint cho Gateway, AuctionService và OrderService kiểm tra quyền thao tác và cập nhật uy tín thời gian thực.
/// </summary>
[ApiController]
[Route("api/v1/internal/users")]
public sealed class InternalUsersController : ControllerBase
{
    private readonly IAdminPenaltyService? _penaltyService;
    private readonly IInternalUserService? _internalUserService;
    private readonly ILogger<InternalUsersController> _logger;

    public InternalUsersController(
        IAdminPenaltyService? penaltyService,
        ILogger<InternalUsersController> logger,
        IInternalUserService? internalUserService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _penaltyService = penaltyService;
        _internalUserService = internalUserService;
    }

    /// <summary>
    /// Kiểm tra quyền hạn thực hiện hành vi của người dùng dựa trên các án phạt đang hiệu lực.
    /// </summary>
    [HttpGet("{userId:guid}/permission-check")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserActionPermissionCheckResponseDto))]
    public async Task<IActionResult> CheckUserActionPermission(
        [FromRoute] Guid userId,
        [FromQuery] string action,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Internal CheckUserActionPermission] Checking permission for UserId: '{UserId}', Action: '{Action}'", userId, action);
        if (_penaltyService == null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "Penalty service is not available.");
        }
        var result = await _penaltyService.CheckUserActionPermissionAsync(userId, action, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [INT-04] Kiểm tra điều kiện điểm uy tín Seller để tạo và mở phiên đấu giá.
    /// </summary>
    [HttpPost("check-reputation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> CheckReputation(
        [FromBody] CheckReputationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Internal CheckReputation] Checking seller eligibility for UserId: '{UserId}'", request.UserId);
        var result = await _internalUserService!.CheckReputationAsync(request, cancellationToken);
        return Ok(new { data = result });
    }

    /// <summary>
    /// [INT-01] Kiểm tra điều kiện điểm uy tín Buyer để đặt giá thầu trong phiên đấu giá.
    /// </summary>
    [HttpPost("verify-bid-eligibility")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> VerifyBidEligibility(
        [FromBody] VerifyBidEligibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Internal VerifyBidEligibility] Verifying bid eligibility for UserId: '{UserId}', AuctionId: '{AuctionId}'", request.UserId, request.AuctionId);
        var result = await _internalUserService!.VerifyBidEligibilityAsync(request, cancellationToken);
        return Ok(new { data = result });
    }

    /// <summary>
    /// [INT-02] Truy vấn thông tin hiển thị công khai hàng loạt theo danh sách UserIds.
    /// </summary>
    [HttpPost("batch-public-info")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetBatchPublicInfo(
        [FromBody] BatchPublicInfoRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Internal BatchPublicInfo] Fetching public info for {Count} users", request.UserIds.Count);
        var result = await _internalUserService!.GetBatchPublicInfoAsync(request, cancellationToken);
        return Ok(new { data = result.Users });
    }

    /// <summary>
    /// [INT-06] Ghi nhận chế tài trừ điểm uy tín tự động khi vi phạm giao dịch (bùng thanh toán đấu giá / hủy phiên).
    /// </summary>
    [HttpPost("penalties")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InternalPenaltyResponse))]
    public async Task<ActionResult<InternalPenaltyResponse>> ApplyInternalPenalty(
        [FromBody] InternalPenaltyRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Internal ApplyPenalty] Applying penalty for UserId: '{UserId}', ReferenceId: '{RefId}'", request.UserId, request.ReferenceId);
        var result = await _internalUserService!.ApplyInternalPenaltyAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [INT-07] Cộng điểm uy tín tự động khi hoàn tất đơn hàng (có bảo vệ Idempotency và chống cày điểm 7 ngày).
    /// </summary>
    [HttpPost("reputation-adjust")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InternalReputationAdjustResponse))]
    public async Task<ActionResult<InternalReputationAdjustResponse>> ApplyInternalReputationAdjust(
        [FromBody] InternalReputationAdjustRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Internal ReputationAdjust] Adjusting reputation for UserId: '{UserId}', ReferenceId: '{RefId}'", request.UserId, request.ReferenceId);
        var result = await _internalUserService!.ApplyInternalReputationAdjustAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>[INT-19] Tra cứu hồ sơ đặt cọc đấu giá của Buyer (trạng thái tài khoản, uy tín, violation streak, hạn chế đấu giá).</summary>
    [HttpGet("{userId:guid}/auction-deposit-profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuctionDepositProfileResponse))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuctionDepositProfileResponse>> GetAuctionDepositProfile(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Internal GetAuctionDepositProfile] Fetching auction deposit profile for UserId: '{UserId}'", userId);
        var result = await _internalUserService!.GetAuctionDepositProfileAsync(userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>[INT-20] Ghi nhận vi phạm đấu giá (không thanh toán sau khi thắng), tăng Streak, trừ uy tín và áp chế tài đấu giá idempotent.</summary>
    [HttpPost("{userId:guid}/auction-violations")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(RecordAuctionViolationResponse))]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RecordAuctionViolationResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RecordAuctionViolationResponse>> RecordAuctionViolation(
        [FromRoute] Guid userId,
        [FromBody] RecordAuctionViolationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Internal RecordAuctionViolation] Recording violation for UserId: '{UserId}', AuctionId: '{AuctionId}', Type: '{ViolationType}'",
            userId,
            request.AuctionId,
            request.ViolationType);

        var result = await _internalUserService!.RecordAuctionViolationAsync(userId, request, cancellationToken);
        return result.Created
            ? StatusCode(StatusCodes.Status201Created, result)
            : Ok(result);
    }
}
