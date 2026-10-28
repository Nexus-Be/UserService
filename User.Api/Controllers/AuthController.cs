namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Application.Services.Auth;

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

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RegisterApiResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterApiRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST Register] Incoming request for Email: '{Email}', Username: '{Username}'", request.Email, request.Username);
        var response = await _authService.RegisterUserAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("otp/verify")]
    [HttpPost("verify-otp")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VerifyOtpApiResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpApiRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST VerifyOtp] Incoming request for Email: '{Email}'", request.Email);
        var response = await _authService.VerifyUserOtpAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("otp/resend")]
    [HttpPost("resend-otp")]
    [HttpPost("otp/send-email")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResendOtpApiResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpApiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { title = "Invalid input", detail = "Email is required.", errorCode = "VAL_INVALID_INPUT" });
        }

        _logger.LogInformation("[REST ResendOtp] Incoming request for Email: '{Email}'", request.Email);
        var success = await _authService.ResendOtpAsync(request.Email, cancellationToken);

        return Ok(new ResendOtpApiResponse(
            Success: success,
            Message: "New OTP verification code has been dispatched to your email address.",
            Email: request.Email
        ));
    }

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LoginApiResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginApiRequest request, CancellationToken cancellationToken)
    {
        var usernameOrEmail = request.Username ?? request.Email;
        if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { title = "Invalid input", detail = "Username and password are required.", errorCode = "VAL_INVALID_INPUT" });
        }

        _logger.LogInformation("[REST Login] Incoming login for: '{UsernameOrEmail}'", usernameOrEmail);
        var dto = new LoginDto(usernameOrEmail, request.Password);
        var result = await _authService.LoginAsync(dto, cancellationToken);

        return Ok(new LoginApiResponse(
            AccessToken: result.AccessToken,
            RefreshToken: result.RefreshToken ?? string.Empty,
            ExpiresAt: result.ExpiresAt,
            UserId: result.UserId,
            Roles: result.Roles
        ));
    }

    [HttpPost("refresh")]
    [HttpPost("refresh-token")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RefreshTokenApiResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenApiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new { title = "Invalid input", detail = "RefreshToken is required.", errorCode = "VAL_INVALID_INPUT" });
        }

        _logger.LogInformation("[REST RefreshToken] Refreshing token");
        var dto = new RefreshTokenDto(request.RefreshToken, request.DeviceInfo);
        var result = await _authService.RefreshTokenAsync(dto, cancellationToken);

        return Ok(new RefreshTokenApiResponse(
            AccessToken: result.AccessToken,
            RefreshToken: result.RefreshToken,
            ExpiresAt: result.ExpiresAt,
            UserId: result.UserId
        ));
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutApiRequest? request, CancellationToken cancellationToken)
    {
        var token = request?.RefreshToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            _logger.LogInformation("[REST Logout] Revoking refresh token");
            await _authService.LogoutAsync(token, cancellationToken);
        }

        return NoContent();
    }

    [HttpPost("logout-all")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LogoutAllApiResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        var userIdHeader = Request.Headers["x-user-id"].ToString();
        if (!Guid.TryParse(userIdHeader, out var userId))
        {
            return BadRequest(new { title = "Invalid user identifier", detail = "Missing or invalid x-user-id header.", errorCode = "VAL_INVALID_USER_ID" });
        }

        _logger.LogInformation("[REST LogoutAll] Revoking all sessions for UserId: '{UserId}'", userId);
        var count = await _authService.LogoutAllAsync(userId, cancellationToken);

        return Ok(new LogoutAllApiResponse(count));
    }

    [HttpPost("password/forgot")]
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ForgotPasswordApiResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordApiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { title = "Invalid input", detail = "Email is required.", errorCode = "VAL_INVALID_INPUT" });
        }

        _logger.LogInformation("[REST ForgotPassword] Processing for Email: '{Email}'", request.Email);
        var result = await _authService.ForgotPasswordAsync(new ForgotPasswordDto(request.Email), cancellationToken);

        return Ok(new ForgotPasswordApiResponse(result.Email));
    }

    [HttpPost("password/reset")]
    [HttpPut("password/reset")]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResetPasswordApiResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordApiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.OtpCode) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { title = "Invalid input", detail = "Email, OTP code and new password are required.", errorCode = "VAL_INVALID_INPUT" });
        }

        _logger.LogInformation("[REST ResetPassword] Resetting password for Email: '{Email}'", request.Email);
        var dto = new ResetPasswordDto(request.Email, request.OtpCode, request.NewPassword, request.ConfirmPassword ?? request.NewPassword);
        var result = await _authService.ResetPasswordAsync(dto, cancellationToken);

        return Ok(new ResetPasswordApiResponse(result.Success));
    }

    [HttpPost("password/change")]
    [HttpPatch("password")]
    [HttpPatch("password/change")]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ChangePasswordApiResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordApiRequest request, CancellationToken cancellationToken)
    {
        var userIdHeader = Request.Headers["x-user-id"].ToString();
        if (!Guid.TryParse(userIdHeader, out var userId))
        {
            return BadRequest(new { title = "Invalid user identifier", detail = "Missing or invalid x-user-id header.", errorCode = "VAL_INVALID_USER_ID" });
        }

        if (string.IsNullOrWhiteSpace(request.OldPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { title = "Invalid input", detail = "Old password and new password are required.", errorCode = "VAL_INVALID_INPUT" });
        }

        _logger.LogInformation("[REST ChangePassword] Changing password for UserId: '{UserId}'", userId);
        var dto = new ChangePasswordDto(request.OldPassword, request.NewPassword, request.ConfirmPassword ?? request.NewPassword);
        var result = await _authService.ChangePasswordAsync(userId, dto, cancellationToken);

        return Ok(new ChangePasswordApiResponse(result.Success));
    }
}
