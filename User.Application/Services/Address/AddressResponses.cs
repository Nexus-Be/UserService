using System;

namespace User.Application.Services.Address;

public sealed record UserAddressDto(
    Guid Id,
    Guid UserId,
    string RecipientName,
    string Phone,
    string? Label,
    int ProvinceId,
    string ProvinceName,
    int DistrictId,
    string DistrictName,
    string WardCode,
    string WardName,
    string AddressLine,
    bool IsDefault,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
