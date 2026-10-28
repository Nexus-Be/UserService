namespace Nexus.Contracts.Events;

using System;

public sealed record SellerApprovedIntegrationEvent(
    Guid SellerId,
    Guid UserId,
    string ShopName,
    DateTimeOffset ApprovedAt
);
