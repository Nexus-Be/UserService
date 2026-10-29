namespace User.Application.Services.Profile;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using User.Application.Common.Converters;
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

public sealed record AddressInputDto(
    [property: JsonPropertyName("province_id")] int? ProvinceId = null,
    [property: JsonPropertyName("district_id")] int? DistrictId = null,
    [property: JsonPropertyName("ward_code")] string? WardCode = null,
    [property: JsonPropertyName("street_address")] string? StreetAddress = null);

public record UpdateProfileDto(
    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null,
    [property: JsonConverter(typeof(FlexibleAddressJsonConverter))]
    string? Address = null,
    string? AvatarUrl = null,
    Gender? Gender = null,
    DateTimeOffset? DateOfBirth = null,
    string? DisplayName = null);

public sealed record UpdateProfileApiRequest : UpdateProfileDto
{
    public UpdateProfileApiRequest(
        string? DisplayName = null,
        string? FirstName = null,
        string? LastName = null,
        string? PhoneNumber = null,
        string? AvatarUrl = null,
        string? Address = null,
        Gender? Gender = null,
        DateTimeOffset? DateOfBirth = null)
        : base(FirstName, LastName, PhoneNumber, Address, AvatarUrl, Gender, DateOfBirth, DisplayName)
    {
    }

    public UpdateProfileApiRequest() : base()
    {
    }
}
