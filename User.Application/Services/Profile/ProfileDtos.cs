namespace User.Application.Services.Profile;

using System.Collections.Generic;

public record UserProfileDto(
    string Id,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string DisplayName,
    string? PhoneNumber,
    string? Address,
    string? AvatarUrl,
    string Status,
    IReadOnlyList<string> Roles,
    string? ShopName,
    int ReputationScore,
    string TrustLevel);
