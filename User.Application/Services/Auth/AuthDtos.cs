namespace User.Application.Services.Auth;

public record RegisterDto(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber = null);

public record VerifyOtpDto(
    string Email,
    string Otp);

public record ResendOtpDto(
    string Email);
