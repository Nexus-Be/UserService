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
    System.Collections.Generic.IReadOnlyList<string> Roles);
