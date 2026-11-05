namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Api.Filters;
using User.Application.Services.Auth;

/// <summary>
/// Quản lý xác thực định danh và phiên làm việc của người dùng (Authentication và Session Management).
/// Bao gồm đăng ký, xác thực OTP, đăng nhập cấp JWT, làm mới token, đăng xuất và quản lý mật khẩu.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// [Public] Đăng ký tài khoản người dùng mới (vai trò mặc định: Buyer) và kích hoạt gửi OTP xác thực email.
    /// </summary>
    /// <param name="request">Payload đăng ký: Email, Mật khẩu, Họ tên, Username, Số điện thoại.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>ID người dùng vừa tạo và thông báo mã OTP đã gửi.</returns>
    /// <response code="201">Đăng ký thành công, mã OTP đã gửi về email.</response>
    /// <response code="400">VAL_INVALID_INPUT nếu dữ liệu đầu vào không hợp lệ.</response>
    /// <response code="409">EMAIL_ALREADY_EXISTS hoặc USERNAME_ALREADY_EXISTS nếu thông tin đã được đăng ký.</response>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(RegisterApiResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterApiRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST Register] Incoming request for Email: '{Email}', Username: '{Username}'", request?.Email, request?.Username);
        var response = await _authService.RegisterUserAsync(request!, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// [Public / Dev] Đăng ký tài khoản kiểm thử phục vụ môi trường phát triển (trả về mã OTP trực tiếp trong response).
    /// </summary>
    /// <param name="request">Payload đăng ký kiểm thử.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông tin tài khoản kèm mã OTP để chạy test tự động.</returns>
    /// <response code="201">Đăng ký kiểm thử thành công.</response>
    [HttpPost("/api/v1/users/auth/test")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(RegisterTestApiResponse))]
    public async Task<IActionResult> RegisterTest([FromBody] RegisterTestApiRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST RegisterTest] Incoming test registration for: '{Email}'", request?.Email);
        var response = await _authService.RegisterTestUserAsync(request!, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// [Public] Xác thực mã OTP gửi về email để kích hoạt tài khoản đăng ký (chuyển trạng thái sang Active).
    /// </summary>
    /// <param name="request">Payload chứa email và mã OTP 6 chữ số.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Kết quả kích hoạt tài khoản thành công.</returns>
    /// <response code="200">Kích hoạt tài khoản thành công, người dùng có thể đăng nhập ngay.</response>
    /// <response code="400">OTP_INVALID hoặc OTP_EXPIRED.</response>
    [HttpPost("otp/verify")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VerifyOtpApiResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpApiRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST VerifyOtp] Incoming request for Email: '{Email}'", request?.Email);
        var response = await _authService.VerifyUserOtpAsync(request!, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// [Public] Yêu cầu gửi lại mã OTP xác thực email khi mã cũ bị thất lạc hoặc hết hạn.
    /// </summary>
    /// <param name="request">Payload chứa email cần nhận lại OTP.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông báo gửi lại thành công và thời gian chờ (cooldown 60s).</returns>
    /// <response code="200">Gửi lại mã OTP thành công.</response>
    /// <response code="400">VAL_INVALID_EMAIL hoặc tài khoản không tồn tại.</response>
    [HttpPost("otp/resend")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResendOtpApiResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpApiRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST ResendOtp] Incoming request for Email: '{Email}'", request?.Email);
        await _authService.ResendOtpAsync(request?.Email ?? string.Empty, cancellationToken);

        return Ok(new ResendOtpApiResponse(
            Email: request?.Email ?? string.Empty,
            ResendCooldown: 60
        ));
    }

    /// <summary>
    /// [Public] Xác thực đăng nhập bằng Email/Username và Mật khẩu, cấp cặp JWT Access Token và Refresh Token.
    /// </summary>
    /// <param name="request">Payload đăng nhập: Identifier (Email/Username) và Password.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Cặp JWT Access Token, Refresh Token, thời hạn hết hạn và hồ sơ người dùng.</returns>
    /// <response code="200">Đăng nhập thành công.</response>
    /// <response code="400">VAL_INVALID_CREDENTIALS hoặc INVALID_CREDENTIALS.</response>
    /// <response code="403">USER_BANNED (kèm lý do cấm) hoặc USER_PENDING_VERIFICATION (chưa kích hoạt OTP).</response>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LoginApiResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] LoginApiRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST Login] Incoming login for: '{Identifier}'", request?.Identifier);
        var dto = new LoginDto(request?.Identifier ?? string.Empty, request?.Password ?? string.Empty);
        var result = await _authService.LoginAsync(dto, cancellationToken);

        return Ok(new LoginApiResponse(
            AccessToken: result.AccessToken,
            RefreshToken: result.RefreshToken,
            TokenType: result.TokenType,
            ExpiresIn: result.ExpiresIn,
            User: result.User,
            UserId: result.UserId ?? result.User.Id,
            Email: result.Email ?? result.User.Email,
            Role: result.Role ?? result.User.Role,
            Roles: result.Roles ?? result.User.Roles,
            Permissions: result.Permissions ?? result.User.Permissions
        ));
    }

    /// <summary>
    /// [Public] Cấp mới Access Token (JWT) bằng Refresh Token hợp lệ (Token Rotation).
    /// </summary>
    /// <param name="request">Payload chứa Refresh Token và DeviceInfo.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Access Token mới và Refresh Token mới.</returns>
    /// <response code="200">Làm mới token thành công.</response>
    /// <response code="401">REFRESH_TOKEN_INVALID hoặc REFRESH_TOKEN_REVOKED.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RefreshTokenApiResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenApiRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST RefreshToken] Refreshing token");
        var dto = new RefreshTokenDto(request?.RefreshToken ?? string.Empty, request?.DeviceInfo);
        var result = await _authService.RefreshTokenAsync(dto, cancellationToken);

        return Ok(new RefreshTokenApiResponse(
            AccessToken: result.AccessToken,
            RefreshToken: result.RefreshToken,
            ExpiresIn: result.ExpiresIn,
            UserId: result.UserId,
            Email: result.Email,
            Role: result.Role,
            Roles: result.Roles,
            Permissions: result.Permissions,
            User: result.User
        ));
    }

    /// <summary>
    /// [Authenticated] Đăng xuất và vô hiệu hóa phiên làm việc của Refresh Token hiện tại.
    /// </summary>
    /// <param name="request">Payload tùy chọn chứa Refresh Token cần thu hồi.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Xác nhận đăng xuất thành công.</returns>
    /// <response code="200">Đăng xuất thành công.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LogoutApiResponse))]
    public async Task<IActionResult> Logout([FromBody] LogoutApiRequest? request, CancellationToken cancellationToken)
    {
        var rawToken = request?.RefreshToken?.Trim();
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            var authHeader = Request.Headers.Authorization.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                rawToken = authHeader["Bearer ".Length..].Trim();
            }
        }

        _logger.LogInformation("[REST Logout] Revoking token");
        await _authService.LogoutAsync(rawToken ?? string.Empty, cancellationToken);

        return Ok(new LogoutApiResponse(Success: true));
    }

    /// <summary>
    /// [Authenticated] Đăng xuất trên toàn bộ thiết bị (thu hồi tất cả Refresh Tokens của người dùng).
    /// </summary>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Số lượng phiên đăng nhập đã bị vô hiệu hóa.</returns>
    /// <response code="200">Đăng xuất toàn bộ thiết bị thành công.</response>
    [HttpPost("logout-all")]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LogoutAllApiResponse))]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST LogoutAll] Revoking all sessions for UserId: '{UserId}'", userId);
        var count = await _authService.LogoutAllAsync(userId, cancellationToken);

        return Ok(new LogoutAllApiResponse(count));
    }

    /// <summary>
    /// [Public] Yêu cầu quên mật khẩu và gửi mã OTP đặt lại mật khẩu về email đã đăng ký.
    /// </summary>
    /// <param name="request">Payload chứa email của tài khoản.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông báo gửi mã OTP thành công.</returns>
    /// <response code="200">Gửi OTP đặt lại mật khẩu thành công.</response>
    /// <response code="400">VAL_INVALID_EMAIL hoặc email không tồn tại.</response>
    [HttpPost("password/forgot")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ForgotPasswordApiResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordApiRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST ForgotPassword] Processing for Email: '{Email}'", request?.Email);
        var result = await _authService.ForgotPasswordAsync(new ForgotPasswordDto(request?.Email ?? string.Empty), cancellationToken);

        return Ok(new ForgotPasswordApiResponse(result.Email));
    }

    /// <summary>
    /// [Public] Đặt lại mật khẩu mới cho tài khoản thông qua mã OTP xác nhận gửi về email.
    /// </summary>
    /// <param name="request">Payload chứa email, mã OTP xác nhận và mật khẩu mới.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Xác nhận đặt lại mật khẩu thành công.</returns>
    /// <response code="200">Đặt lại mật khẩu thành công, toàn bộ phiên đăng nhập cũ bị thu hồi.</response>
    /// <response code="400">OTP_INVALID, OTP_EXPIRED hoặc mật khẩu mới không hợp lệ.</response>
    [HttpPut("password/reset")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResetPasswordApiResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordApiRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST ResetPassword] Resetting password for Email: '{Email}'", request?.Email);
        var dto = new ResetPasswordDto(request?.Email ?? string.Empty, request?.OtpCode ?? string.Empty, request?.NewPassword ?? string.Empty, request?.ConfirmPassword ?? request?.NewPassword ?? string.Empty);
        var result = await _authService.ResetPasswordAsync(dto, cancellationToken);

        return Ok(new ResetPasswordApiResponse(result.Success));
    }

    /// <summary>
    /// [Authenticated] Thay đổi mật khẩu của tài khoản đang đăng nhập (yêu cầu xác thực mật khẩu cũ).
    /// </summary>
    /// <param name="request">Payload chứa mật khẩu cũ, mật khẩu mới và xác nhận mật khẩu mới.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Xác nhận đổi mật khẩu thành công.</returns>
    /// <response code="200">Đổi mật khẩu thành công.</response>
    /// <response code="400">OLD_PASSWORD_INCORRECT hoặc mật khẩu mới trùng mật khẩu cũ.</response>
    [HttpPatch("password")]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ChangePasswordApiResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordApiRequest request, CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST ChangePassword] Changing password for UserId: '{UserId}'", userId);
        var dto = new ChangePasswordDto(request?.OldPassword ?? string.Empty, request?.NewPassword ?? string.Empty, request?.ConfirmPassword ?? request?.NewPassword ?? string.Empty);
        var result = await _authService.ChangePasswordAsync(userId, dto, cancellationToken);

        return Ok(new ChangePasswordApiResponse(result.Success));
    }
}
