namespace Nexus.Contracts.Events;

using System;

public sealed record SellerApprovedIntegrationEvent(
    Guid SellerId,
    Guid UserId,
    string ShopName,
    string? BusinessEmail,
    string Hotline,
    string PickupAddress,
    int ProvinceId,
    int DistrictId,
    string WardCode,
    DateTimeOffset ApprovedAt
);
