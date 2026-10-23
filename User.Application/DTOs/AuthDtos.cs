namespace User.Application.DTOs;

public sealed record RegisterDto(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber = null);

public sealed record VerifyOtpDto(
    string Email,
    string Otp);

public sealed record ResendOtpDto(
    string Email);
