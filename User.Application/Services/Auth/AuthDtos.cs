namespace User.Application.Services.Auth;

using System;

public record RegisterDto(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber = null,
    string? Username = null);

public record VerifyOtpDto(
    string Email,
    string Otp);

public record ResendOtpDto(
    string Email);

public record LoginDto(
    string UsernameOrEmail,
    string Password);

public record LoginResultDto(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    LoginUserApiResponse User,
    string? UserId = null,
    string? Email = null,
    string? Role = null,
    System.Collections.Generic.IReadOnlyList<string>? Roles = null,
    System.Collections.Generic.IReadOnlyList<string>? Permissions = null);

public record RefreshTokenDto(
    string RefreshToken,
    string? DeviceInfo = null,
    string? IpAddress = null);

public record RefreshTokenResultDto(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string? UserId = null,
    string? Email = null,
    string? Role = null,
    System.Collections.Generic.IReadOnlyList<string>? Roles = null,
    System.Collections.Generic.IReadOnlyList<string>? Permissions = null,
    LoginUserApiResponse? User = null);

public record LogoutDto(
    string RefreshToken);

public record LogoutAllDto(
    Guid UserId);

public record ForgotPasswordDto(
    string Email);

public record ForgotPasswordResultDto(
    string Email,
    string Message,
    int OtpExpiresIn);

public record ResetPasswordDto(
    string Email,
    string OtpCode,
    string NewPassword,
    string ConfirmPassword);

public record ResetPasswordResultDto(
    bool Success,
    string Message);

public record ChangePasswordDto(
    string OldPassword,
    string NewPassword,
    string ConfirmPassword);

public record ChangePasswordResultDto(
    bool Success,
    string Message);
