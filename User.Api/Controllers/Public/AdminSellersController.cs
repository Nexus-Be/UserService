namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Api.Filters;
using User.Application.Services.Seller;

/// <summary>
/// Quản trị hồ sơ người bán và kiểm soát trạng thái gian hàng dành cho Admin và Support Staff.
/// </summary>
[ApiController]
[Route("api/v1/admin/sellers")]
[RequireRole("admin", "staff")]
public sealed class AdminSellersController : ControllerBase
{
    private readonly ISellerService _sellerService;
    private readonly ILogger<AdminSellersController> _logger;

    public AdminSellersController(
        ISellerService sellerService,
        ILogger<AdminSellersController> logger)
    {
        _sellerService = sellerService ?? throw new ArgumentNullException(nameof(sellerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// [Admin / Staff] Tra cứu thông tin chi tiết hồ sơ người bán, bao gồm KYC, thông tin kho hàng và điểm uy tín.
    /// </summary>
    /// <param name="sellerId">Định danh GUID của hồ sơ người bán (SellerProfile.Id).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ người bán đầy đủ cho mục đích kiểm toán và giám sát.</returns>
    /// <response code="200">Tìm thấy và trả về thông tin chi tiết người bán.</response>
    /// <response code="404">SELLER_NOT_FOUND nếu không tìm thấy hồ sơ.</response>
    [HttpGet("{sellerId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminSellerProfileResponseDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAdminSellerProfile(
        [FromRoute] Guid sellerId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST Admin GetAdminSellerProfile] Fetching seller profile for identifier: '{SellerId}'", sellerId);
        var result = await _sellerService.GetAdminSellerProfileAsync(sellerId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Admin / Staff] Tạm đình chỉ quyền bán hàng của gian hàng (ShopStatus = Suspended). Quyền mua sắm cá nhân của người bán vẫn được bảo toàn.
    /// </summary>
    /// <param name="sellerId">Định danh GUID của hồ sơ người bán cần đình chỉ.</param>
    /// <param name="dto">Payload chứa lý do đình chỉ (Reason).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông tin xác nhận đình chỉ và thời điểm áp dụng.</returns>
    /// <response code="200">Đình chỉ gian hàng thành công, CanSell bị vô hiệu hóa, CanPlaceOrder giữ nguyên.</response>
    /// <response code="400">VAL_INVALID_INPUT nếu thiếu lý do đình chỉ.</response>
    /// <response code="404">SELLER_NOT_FOUND nếu không tìm thấy người bán.</response>
    [HttpPut("{sellerId:guid}/suspend")]
    [RequireRole("admin", "staff")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SuspendSellerResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendSeller(
        [FromRoute] Guid sellerId,
        [FromBody] SuspendSellerDto dto,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var actorId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin/Staff SuspendSeller] Suspending seller '{SellerId}' by actor '{ActorId}'", sellerId, actorId);
        var result = await _sellerService.SuspendSellerAsync(sellerId, dto, actorId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Admin / Staff] Khôi phục quyền bán hàng cho gian hàng đã bị đình chỉ (ShopStatus = Active).
    /// </summary>
    /// <param name="sellerId">Định danh GUID của hồ sơ người bán cần kích hoạt lại.</param>
    /// <param name="dto">Payload tùy chọn chứa lý do kích hoạt lại.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông tin xác nhận kích hoạt lại gian hàng.</returns>
    /// <response code="200">Kích hoạt lại gian hàng thành công, CanSell được bật lại.</response>
    /// <response code="400">SELLER_NOT_SUSPENDED nếu gian hàng không ở trạng thái bị đình chỉ.</response>
    /// <response code="404">SELLER_NOT_FOUND nếu không tìm thấy người bán.</response>
    [HttpPut("{sellerId:guid}/reactivate")]
    [RequireRole("admin", "staff")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReactivateSellerResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReactivateSeller(
        [FromRoute] Guid sellerId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] ReactivateSellerDto? dto,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var actorId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin/Staff ReactivateSeller] Reactivating seller '{SellerId}' by actor '{ActorId}'", sellerId, actorId);
        var result = await _sellerService.ReactivateSellerAsync(sellerId, dto, actorId, cancellationToken);
        return Ok(result);
    }
}
