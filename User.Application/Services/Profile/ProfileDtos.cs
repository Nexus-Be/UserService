namespace User.Application.Services.Profile;

using System;
using System.Collections.Generic;
using User.Domain.Enums;

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

public record UpdateProfileDto(
    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null,
    string? Address = null,
    string? AvatarUrl = null,
    Gender? Gender = null,
    DateTimeOffset? DateOfBirth = null);

