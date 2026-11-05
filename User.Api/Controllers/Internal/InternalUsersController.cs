namespace User.Api.Controllers.Internal;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Application.Services.Admin;
using User.Application.Services.Internal;

/// <summary>
/// Controller nội bộ phục vụ giao tiếp liên service (Internal Inter-Service Communication).
/// Cung cấp endpoint cho AuctionService, Gateway và các service khác kiểm tra điều kiện đấu giá, đặt giá và tra cứu thông tin công khai.
/// </summary>
[ApiController]
[Route("api/v1/internal/users")]
public sealed class InternalUsersController : ControllerBase
{
    private readonly IAdminPenaltyService _penaltyService;
    private readonly IInternalUserService _internalUserService;
    private readonly ILogger<InternalUsersController> _logger;

    public InternalUsersController(
        IAdminPenaltyService penaltyService,
        IInternalUserService internalUserService,
        ILogger<InternalUsersController> logger)
    {
        _penaltyService = penaltyService ?? throw new ArgumentNullException(nameof(penaltyService));
        _internalUserService = internalUserService ?? throw new ArgumentNullException(nameof(internalUserService));
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

    /// <summary>
    /// Kiểm tra uy tín và tư cách người bán (Seller) khi tạo phiên đấu giá (Auction Service).
    /// </summary>
    /// <param name="request">Payload chứa action (SELL) và userId.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Kết quả kiểm tra allowed, isApprovedSeller, userStatus và failureReason.</returns>
    /// <response code="200">Kiểm tra hoàn tất, trả về kết quả cho phép hoặc từ chối kèm failureReason.</response>
    [HttpPost("check-reputation")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CheckReputationResponse))]
    public async Task<IActionResult> CheckReputation(
        [FromBody] CheckReputationRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Internal CheckReputation] Action: '{Action}', UserId: '{UserId}'", request.Action, request.UserId);
        var result = await _internalUserService.CheckReputationAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Xác thực tư cách người mua (Buyer) khi đặt giá trong phiên đấu giá (SLA &lt; 800ms).
    /// </summary>
    /// <param name="request">Payload chứa userId người đặt giá.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Kết quả allowed và failureReason.</returns>
    /// <response code="200">Kiểm tra hoàn tất, trả về kết quả cho phép hoặc từ chối kèm failureReason.</response>
    [HttpPost("verify-bid-eligibility")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VerifyBidEligibilityResponse))]
    public async Task<IActionResult> VerifyBidEligibility(
        [FromBody] VerifyBidEligibilityRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Internal VerifyBidEligibility] UserId: '{UserId}'", request.UserId);
        var result = await _internalUserService.VerifyBidEligibilityAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Tra cứu danh sách thông tin công khai và tên đã che (maskedName) phục vụ hiển thị lịch sử đấu giá (tối đa 100 User).
    /// </summary>
    /// <param name="request">Danh sách các userId cần tra cứu (tối đa 100 ID).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Mảng danh sách các UserPublicInfoDto chứa userId, maskedName, isDeleted.</returns>
    /// <response code="200">Tra cứu thành công, trả về mảng kết quả.</response>
    [HttpPost("batch-public-info")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<UserPublicInfoDto>))]
    public async Task<IActionResult> GetBatchPublicInfo(
        [FromBody] BatchPublicInfoRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Internal GetBatchPublicInfo] Count: {Count}", request.UserIds?.Count ?? 0);
        var result = await _internalUserService.GetBatchPublicInfoAsync(request, cancellationToken);
        return Ok(result);
    }
}
