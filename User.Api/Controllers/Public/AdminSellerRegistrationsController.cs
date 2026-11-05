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
using User.Application.Common.Models;
using User.Application.Services.Seller;

/// <summary>
/// Thẩm định và xét duyệt đơn đăng ký nâng cấp Người bán (Seller Onboarding và KYC Review).
/// Dành riêng cho nhân viên Support Staff theo nguyên tắc Phân tách trách nhiệm (SoD).
/// </summary>
[ApiController]
[Route("api/v1/admin/seller-registrations")]
[RequireRole("staff")]
public sealed class AdminSellerRegistrationsController : ControllerBase
{
    private readonly ISellerService _sellerService;
    private readonly ILogger<AdminSellerRegistrationsController> _logger;

    public AdminSellerRegistrationsController(ISellerService sellerService, ILogger<AdminSellerRegistrationsController> logger)
    {
        _sellerService = sellerService;
        _logger = logger;
    }

    /// <summary>
    /// [Staff] Tra cứu danh sách các đơn đăng ký người bán đang chờ thẩm định có phân trang.
    /// </summary>
    /// <param name="pagination">Thông số phân trang chuẩn: Page và PageSize.</param>
    /// <param name="status">Lọc theo trạng thái đơn: Pending, UnderReview, Approved, Rejected.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Danh sách đơn đăng ký người bán kèm siêu dữ liệu phân trang.</returns>
    /// <response code="200">Trả về danh sách đơn đăng ký thành công.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedData<AdminSellerRegistrationItemDto>))]
    public async Task<IActionResult> ListSellerRegistrations(
        [FromQuery] PageQuery pagination,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = pagination.Normalize();

        _logger.LogInformation("[REST ListSellerRegistrations] Fetching page {Page}, pageSize {PageSize}, status {Status}", safePage, safePageSize, status);
        var result = await _sellerService.GetSellerRegistrationsAsync(safePage, safePageSize, status, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Staff] Xem chi tiết hồ sơ đơn đăng ký người bán bao gồm CCCD, giấy phép kinh doanh và địa chỉ kho để thẩm định KYC.
    /// </summary>
    /// <param name="registrationId">Định danh GUID của đơn đăng ký.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ KYC chi tiết của đơn đăng ký.</returns>
    /// <response code="200">Tìm thấy và trả về chi tiết hồ sơ đăng ký.</response>
    /// <response code="404">REGISTRATION_NOT_FOUND nếu không tìm thấy đơn.</response>
    [HttpGet("{registrationId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SellerRegistrationDetailsDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSellerRequestDetails(
        [FromRoute] Guid registrationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST GetSellerRequestDetails] Fetching registration details for '{RegistrationId}'", registrationId);
        var result = await _sellerService.GetSellerRegistrationDetailsAsync(registrationId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Staff] Phê duyệt hồ sơ người bán hợp lệ: đồng bộ kho hàng, chuyển vai trò Buyer -> Seller và thu hồi refresh tokens tức thì.
    /// </summary>
    /// <param name="id">Định danh GUID của đơn đăng ký cần duyệt.</param>
    /// <param name="body">Payload duyệt bổ sung (tùy chọn).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Kết quả xác nhận phê duyệt người bán thành công.</returns>
    /// <response code="200">Duyệt hồ sơ thành công, kích hoạt gian hàng và chuyển role sang Seller.</response>
    /// <response code="400">REGISTRATION_NOT_PENDING nếu đơn đã được xử lý trước đó.</response>
    /// <response code="404">REGISTRATION_NOT_FOUND nếu không tìm thấy đơn.</response>
    [HttpPut("{id:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApproveSellerRegistrationResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveSellerRegistration(
        [FromRoute] Guid id,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] ApproveSellerRegistrationDto? body,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var reviewerId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST ApproveSellerRegistration] Approving registration '{Id}' by Staff '{StaffId}'", id, reviewerId);
        var result = await _sellerService.ApproveSellerRegistrationAsync(id, reviewerId, body, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Staff] Từ chối hồ sơ người bán không đủ điều kiện (bắt buộc phải kèm lý do từ chối rõ ràng).
    /// </summary>
    /// <param name="id">Định danh GUID của đơn đăng ký cần từ chối.</param>
    /// <param name="body">Payload bắt buộc chứa lý do từ chối (RejectionReason).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Kết quả xác nhận từ chối đơn đăng ký.</returns>
    /// <response code="200">Từ chối đơn đăng ký thành công và gửi email thông báo lý do.</response>
    /// <response code="400">VAL_INVALID_INPUT nếu thiếu lý do hoặc đơn đã xử lý.</response>
    /// <response code="404">REGISTRATION_NOT_FOUND nếu không tìm thấy đơn.</response>
    [HttpPut("{id:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RejectSellerRegistrationResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectSellerRegistration(
        [FromRoute] Guid id,
        [FromBody] RejectSellerRegistrationDto body,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var reviewerId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST RejectSellerRegistration] Rejecting registration '{Id}' by Staff '{StaffId}'", id, reviewerId);
        var result = await _sellerService.RejectSellerRegistrationAsync(id, reviewerId, body, cancellationToken);
        return Ok(result);
    }
}
