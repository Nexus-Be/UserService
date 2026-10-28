namespace User.Application.Services.Auth;

using System;
using System.Collections.Generic;

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
    string ExpiresAt,
    string UserId,
    IReadOnlyList<string> Roles,
    string? RefreshToken = null);

public record RefreshTokenDto(
    string RefreshToken,
    string? DeviceInfo = null,
    string? IpAddress = null);

public record RefreshTokenResultDto(
    string AccessToken,
    string RefreshToken,
    string ExpiresAt,
    string UserId,
    IReadOnlyList<string> Roles);

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

// ==========================================
// REST API Request & Response Contracts
// ==========================================

public sealed record RegisterApiRequest(
    string Email,
    string Password,
    string? Username = null,
    string? DisplayName = null,
    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null
);

public sealed record RegisterApiResponse(
    string Id,
    string Email,
    string DisplayName,
    string Status,
    string ShopName,
    IReadOnlyList<string> Roles
);

public sealed record VerifyOtpApiRequest(
    string Email,
    string? Otp = null,
    string? ResolvedOtp = null,
    string? OtpCode = null
);

public sealed record VerifyOtpApiResponse(
    string Id,
    string Email,
    string DisplayName,
    string Status,
    string ShopName,
    IReadOnlyList<string> Roles
);

public sealed record ResendOtpApiRequest(
    string Email
);

public sealed record ResendOtpApiResponse(
    bool Success,
    string Message,
    string Email
);

public sealed record LoginApiRequest(
    string? Username,
    string Password,
    string? Email = null,
    string? DeviceInfo = null
);

public sealed record LoginApiResponse(
    string AccessToken,
    string RefreshToken,
    string ExpiresAt,
    string UserId,
    IReadOnlyList<string> Roles
);

public sealed record RefreshTokenApiRequest(
    string RefreshToken,
    string? DeviceInfo = null
);

public sealed record RefreshTokenApiResponse(
    string AccessToken,
    string RefreshToken,
    string ExpiresAt,
    string UserId
);

public sealed record LogoutApiRequest(
    string? RefreshToken = null,
    string? DeviceInfo = null
);

public sealed record LogoutAllApiResponse(
    int CountRevoked
);

public sealed record ForgotPasswordApiRequest(
    string Email
);

public sealed record ForgotPasswordApiResponse(
    string Email
);

public sealed record ResetPasswordApiRequest(
    string Email,
    string OtpCode,
    string NewPassword,
    string? ConfirmPassword = null
);

public sealed record ResetPasswordApiResponse(
    bool Success
);

public sealed record ChangePasswordApiRequest(
    string OldPassword,
    string NewPassword,
    string? ConfirmPassword = null
);

public sealed record ChangePasswordApiResponse(
    bool Success
);
