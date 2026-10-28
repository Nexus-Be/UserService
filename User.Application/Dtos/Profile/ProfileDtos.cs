namespace User.Application.Dtos.Profile;

using System;
using User.Domain.Enums;

public record UpdateProfileDto(
    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null,
    string? Address = null,
    string? AvatarUrl = null,
    Gender? Gender = null,
    DateTimeOffset? DateOfBirth = null,
    string? DisplayName = null,
    string? Username = null);
