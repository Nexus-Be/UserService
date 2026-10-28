namespace User.Application.Dtos.Profile;

using System;
using User.Domain.Enums;

public sealed record UpdateProfileApiRequest(
    string? DisplayName = null,
    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null,
    string? AvatarUrl = null,
    string? Address = null,
    Gender? Gender = null,
    DateTimeOffset? DateOfBirth = null,
    string? Username = null) : UpdateProfileDto(FirstName, LastName, PhoneNumber, Address, AvatarUrl, Gender, DateOfBirth, DisplayName, Username);
