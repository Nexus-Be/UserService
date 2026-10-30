namespace User.Application.Services.Auth;

public sealed record RegisterApiRequest(
    string Email,
    string Password,
    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null,
    string? Username = null
);

public sealed record VerifyOtpApiRequest(
    string Email,
    string Otp
);

public sealed record ResendOtpApiRequest(
    string Email,
    string? Type = null
);

public sealed record LoginApiRequest(
    string Identifier,
    string Password,
    string? DeviceInfo = null,
    string? IpAddress = null
);

public sealed record RefreshTokenApiRequest(
    string RefreshToken,
    string? DeviceInfo = null
);

public sealed record LogoutApiRequest(
    string? RefreshToken = null,
    string? DeviceInfo = null
);

public sealed record ForgotPasswordApiRequest(
    string Email
);

public sealed record ResetPasswordApiRequest(
    string Email,
    string OtpCode,
    string NewPassword,
    string ConfirmPassword
);

public sealed record ChangePasswordApiRequest(
    string OldPassword,
    string NewPassword,
    string ConfirmPassword
);
