namespace User.Application.Services.Auth;

using System.Collections.Generic;

public sealed record RegisterApiResponse(
    string UserId,
    string Email,
    int OtpExpiresIn
);

public sealed record VerifyOtpApiResponse(
    bool IsVerified,
    string Email,
    string? UserId = null,
    string? Role = null,
    IReadOnlyList<string>? Roles = null,
    IReadOnlyList<string>? Permissions = null
);

public sealed record ResendOtpApiResponse(
    string Email,
    int ResendCooldown
);

public sealed record LoginUserApiResponse(
    string Id,
    string Email,
    string? Username,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    string? UserId = null,
    string? Role = null
);

public sealed record LoginApiResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    LoginUserApiResponse User,
    string? UserId = null,
    string? Email = null,
    string? Role = null,
    IReadOnlyList<string>? Roles = null,
    IReadOnlyList<string>? Permissions = null
);

public sealed record RefreshTokenApiResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string? UserId = null,
    string? Email = null,
    string? Role = null,
    IReadOnlyList<string>? Roles = null,
    IReadOnlyList<string>? Permissions = null,
    LoginUserApiResponse? User = null
);

public sealed record LogoutApiResponse(
    bool Success
);

public sealed record LogoutAllApiResponse(
    int CountRevoked
);

public sealed record ForgotPasswordApiResponse(
    string Email
);

public sealed record ResetPasswordApiResponse(
    bool Success
);

public sealed record ChangePasswordApiResponse(
    bool Success
);
