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
/// Quản lý người bán (Seller): Đăng ký nâng cấp từ Buyer, xác thực OTP email doanh nghiệp,
/// theo dõi tiến trình xét duyệt hồ sơ, cập nhật thông tin gian hàng và tra cứu thông tin shop công khai.
/// </summary>
[ApiController]
[Route("api/v1/sellers")]
public sealed class SellerController : ControllerBase
{
    private readonly ISellerService _sellerService;
    private readonly ILogger<SellerController> _logger;

    public SellerController(ISellerService sellerService, ILogger<SellerController> logger)
    {
        _sellerService = sellerService;
        _logger = logger;
    }

    /// <summary>
    /// [Buyer] Nộp hồ sơ đăng ký nâng cấp tài khoản lên Người bán (Seller Onboarding) và gửi mã OTP xác thực email doanh nghiệp.
    /// </summary>
    /// <param name="request">Payload đăng ký: Tên shop, Email doanh nghiệp, Địa chỉ kho lấy hàng, CCCD, Giấy phép kinh doanh.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ đăng ký vừa tạo và thông báo gửi OTP.</returns>
    /// <response code="201">Nộp đơn thành công, mã OTP đã được gửi đến email doanh nghiệp.</response>
    /// <response code="400">VAL_INVALID_INPUT nếu dữ liệu thiếu hoặc không hợp lệ.</response>
    /// <response code="409">SHOP_NAME_TAKEN hoặc SELLER_ALREADY_EXISTS nếu đã có gian hàng hoạt động.</response>
    [HttpPost("registration")]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(SellerRegistrationResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterSeller(
        [FromBody] CreateSellerRegistrationDto request,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST RegisterSeller] Submitting seller onboarding for UserId: '{UserId}', Shop: '{ShopName}'", userId, request?.ShopName);
        var result = await _sellerService.RegisterSellerAsync(userId, request!, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// [Buyer] Xác thực mã OTP gửi về Email doanh nghiệp để chuyển hồ sơ sang trạng thái Chờ duyệt (Pending Review).
    /// </summary>
    /// <param name="request">Payload chứa email doanh nghiệp và mã OTP 6 chữ số.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Kết quả xác thực thành công.</returns>
    /// <response code="200">Xác thực OTP thành công, hồ sơ chuyển sang hàng chờ Support Staff xét duyệt.</response>
    /// <response code="400">OTP_INVALID hoặc OTP_EXPIRED.</response>
    [HttpPost("otp/verify")]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VerifySellerOtpResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifySellerOtp(
        [FromBody] VerifySellerOtpDto request,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST VerifySellerOtp] Verifying OTP for UserId: '{UserId}', Email: '{Email}'", userId, request?.BusinessEmail);
        var result = await _sellerService.VerifySellerRegistrationOtpAsync(userId, request!, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// [Buyer] Yêu cầu gửi lại mã OTP xác thực email doanh nghiệp khi mã cũ hết hạn.
    /// </summary>
    /// <param name="request">Payload chứa email doanh nghiệp nhận lại OTP.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông báo gửi lại OTP thành công.</returns>
    /// <response code="200">Mã OTP mới đã được gửi lại thành công.</response>
    /// <response code="400">VAL_INVALID_INPUT nếu email không khớp với đơn đăng ký.</response>
    [HttpPost("otp/resend")]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendSellerOtp(
        [FromBody] ResendSellerOtpDto request,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST ResendSellerOtp] Resending OTP for UserId: '{UserId}', Email: '{Email}'", userId, request?.BusinessEmail);
        await _sellerService.ResendSellerOtpAsync(userId, request?.BusinessEmail ?? string.Empty, cancellationToken);

        return Ok(new { message = "Verification code resent successfully." });
    }

    /// <summary>
    /// [Buyer] Tra cứu tiến trình và trạng thái đơn đăng ký người bán của chính mình.
    /// </summary>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Trạng thái hồ sơ: Draft (chưa xác thực OTP), Pending (chờ duyệt), Approved (đã duyệt), Rejected (bị từ chối).</returns>
    /// <response code="200">Trả về trạng thái đơn đăng ký thành công.</response>
    /// <response code="404">REGISTRATION_NOT_FOUND nếu người dùng chưa từng nộp hồ sơ.</response>
    [HttpGet("status")]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SellerRegistrationStatusResponseDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSellerRegistrationStatus(CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST GetSellerRegistrationStatus] Checking onboarding status for UserId: '{UserId}'", userId);
        var result = await _sellerService.GetSellerRegistrationStatusAsync(userId, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// [Seller / Admin] Tra cứu thông tin hồ sơ gian hàng của chính mình.
    /// </summary>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ gian hàng: Tên shop, Hotline, Địa chỉ kho lấy hàng, Trạng thái gian hàng (ShopStatus).</returns>
    /// <response code="200">Trả về hồ sơ gian hàng thành công.</response>
    /// <response code="403">FORBIDDEN nếu người dùng chưa có vai trò Seller.</response>
    /// <response code="404">SELLER_PROFILE_NOT_FOUND nếu chưa tạo hồ sơ người bán.</response>
    [HttpGet("profile")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SellerOwnProfileResponseDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSellerOwnProfile(CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST GetSellerOwnProfile] Fetching seller profile for UserId: '{UserId}'", userId);
        var result = await _sellerService.GetSellerOwnProfileAsync(userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Seller / Admin] Cập nhật thông tin gian hàng và địa chỉ kho lấy hàng của chính mình.
    /// </summary>
    /// <param name="request">Payload chứa thông tin cập nhật: Hotline, AvatarUrl, Địa chỉ kho (PickupAddress, WardCode, DistrictId, ProvinceId).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ gian hàng sau khi được cập nhật thành công.</returns>
    /// <response code="200">Cập nhật hồ sơ gian hàng thành công.</response>
    /// <response code="400">VAL_INVALID_INPUT nếu dữ liệu kho hàng không hợp lệ.</response>
    /// <response code="403">FORBIDDEN nếu chưa có quyền Seller.</response>
    /// <response code="404">SELLER_PROFILE_NOT_FOUND.</response>
    [HttpPatch("profile")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SellerOwnProfileUpdatedResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSellerOwnProfile(
        [FromBody] UpdateSellerOwnProfileDto request,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST UpdateSellerOwnProfile] Updating seller profile for UserId: '{UserId}'", userId);
        var result = await _sellerService.UpdateSellerOwnProfileAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Public] Tra cứu thông tin công khai của một gian hàng cho khách hàng xem shop (Storefront).
    /// </summary>
    /// <param name="id">Định danh GUID của gian hàng hoặc ID của tài khoản người bán.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông tin công khai của shop: Tên shop, Avatar, Hotline, Tỉnh/Thành phố, Trạng thái kinh doanh.</returns>
    /// <response code="200">Tìm thấy và trả về thông tin shop công khai.</response>
    /// <response code="404">SELLER_NOT_FOUND nếu không tìm thấy gian hàng.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PublicSellerProfileResponseDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicSellerProfile([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST GetPublicSellerProfile] Loading public seller profile for identifier: '{Id}'", id);
        var result = await _sellerService.GetPublicSellerProfileAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Authenticated] Tải lên ảnh 2 mặt CCCD và Giấy phép kinh doanh phục vụ thẩm định hồ sơ mở Shop.
    /// </summary>
    [HttpPost("registration/documents")]
    [RequireRole]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadSellerDocumentsResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> UploadSellerDocuments(
        IFormFile? frontCccd,
        IFormFile? backCccd,
        IFormFile? businessLicense = null,
        [FromForm] Guid? registrationId = null,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        if (frontCccd == null || frontCccd.Length <= 0 || backCccd == null || backCccd.Length <= 0)
        {
            throw new User.Domain.Exceptions.BusinessRuleValidationException(
                "VAL_INVALID_INPUT",
                "Front and back identity card images are required.");
        }

        await using var frontStream = frontCccd.OpenReadStream();
        await using var backStream = backCccd.OpenReadStream();
        await using var licenseStream = businessLicense?.OpenReadStream();

        var frontInput = new SellerDocumentFileInput(frontStream, frontCccd.FileName, frontCccd.ContentType, frontCccd.Length);
        var backInput = new SellerDocumentFileInput(backStream, backCccd.FileName, backCccd.ContentType, backCccd.Length);
        var licenseInput = businessLicense != null && licenseStream != null
            ? new SellerDocumentFileInput(licenseStream, businessLicense.FileName, businessLicense.ContentType, businessLicense.Length)
            : null;

        _logger.LogInformation("[REST UploadSellerDocuments] Uploading KYC documents for UserId: '{UserId}'", userId);
        var result = await _sellerService.UploadSellerDocumentsAsync(
            userId,
            frontInput,
            backInput,
            licenseInput,
            registrationId,
            cancellationToken);

        return Ok(result);
    }
}
