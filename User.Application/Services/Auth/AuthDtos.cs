namespace User.Application.Services.Auth;

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
    System.Collections.Generic.IReadOnlyList<string> Roles,
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
    System.Collections.Generic.IReadOnlyList<string> Roles);
public record LogoutDto(
    string RefreshToken);

public record LogoutAllDto(
    System.Guid UserId);
