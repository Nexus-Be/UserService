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

    /// <summary>[Public] Registers a new user account and sends a verification OTP.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterApiRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST Register] Incoming request for Email: '{Email}', Username: '{Username}'", request?.Email, request?.Username);
        var response = await _authService.RegisterUserAsync(request!, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>[Public / Dev] Tests user registration by creating a pending Buyer user and dispatching an OTP email.</summary>
    [HttpPost("/api/v1/users/auth/test")]
    public async Task<IActionResult> RegisterTest([FromBody] RegisterTestApiRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST RegisterTest] Incoming test registration for: '{Email}'", request?.Email);
        var response = await _authService.RegisterTestUserAsync(request!, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>[Public] Verifies the OTP sent to a newly registered account.</summary>
    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpApiRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST VerifyOtp] Incoming request for Email: '{Email}'", request?.Email);
        var response = await _authService.VerifyUserOtpAsync(request!, cancellationToken);
        return Ok(response);
    }

    /// <summary>[Public] Resends the account verification OTP email.</summary>
    [HttpPost("otp/resend")]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpApiRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST ResendOtp] Incoming request for Email: '{Email}'", request?.Email);
        await _authService.ResendOtpAsync(request?.Email ?? string.Empty, cancellationToken);

        return Ok(new ResendOtpApiResponse(
            Email: request?.Email ?? string.Empty,
            ResendCooldown: 60
        ));
    }

    /// <summary>[Public] Authenticates a user and issues access and refresh tokens.</summary>
    [HttpPost("login")]
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

    /// <summary>[Public] Issues a new access token from a valid refresh token.</summary>
    [HttpPost("refresh")]
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

    /// <summary>[Authenticated] Revokes the supplied refresh token session.</summary>
    [HttpPost("logout")]
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

    /// <summary>[Authenticated] Revokes all active sessions of the authenticated user.</summary>
    [HttpPost("logout-all")]
    [RequireRole]
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

    /// <summary>[Public] Sends a password reset OTP to the registered email.</summary>
    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordApiRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST ForgotPassword] Processing for Email: '{Email}'", request?.Email);
        var result = await _authService.ForgotPasswordAsync(new ForgotPasswordDto(request?.Email ?? string.Empty), cancellationToken);

        return Ok(new ForgotPasswordApiResponse(result.Email));
    }

    /// <summary>[Public] Resets the account password using a valid OTP code.</summary>
    [HttpPut("password/reset")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordApiRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST ResetPassword] Resetting password for Email: '{Email}'", request?.Email);
        var dto = new ResetPasswordDto(request?.Email ?? string.Empty, request?.OtpCode ?? string.Empty, request?.NewPassword ?? string.Empty, request?.ConfirmPassword ?? request?.NewPassword ?? string.Empty);
        var result = await _authService.ResetPasswordAsync(dto, cancellationToken);

        return Ok(new ResetPasswordApiResponse(result.Success));
    }

    /// <summary>[Authenticated] Changes the password of the authenticated user.</summary>
    [HttpPatch("password")]
    [RequireRole]
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
