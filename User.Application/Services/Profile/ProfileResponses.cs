namespace User.Application.Services.Profile;

using System;
using System.Collections.Generic;

public sealed record UserProfileDto(
    string Id,
    string Email,
    string? Username,
    string FirstName,
    string LastName,
    string DisplayName,
    string? PhoneNumber,
    string? Address,
    string? AvatarUrl,
    string Status,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool IsSeller,
    string? ShopName,
    int ReputationScore,
    string TrustLevel,
    DateTimeOffset CreatedAt);

public sealed record PublicUserProfileDto(
    string Id,
    string? Username,
    string FirstName,
    string LastName,
    string? AvatarUrl,
    int ReputationScore,
    string TrustLevel,
    IReadOnlyList<string> Badges,
    bool IsSeller,
    DateTimeOffset JoinedAt);
