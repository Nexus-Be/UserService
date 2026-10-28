namespace User.Application.Services.Seller;

using System;
using System.Text.Json.Serialization;

public sealed record SellerRegistrationResponseDto(
    [property: JsonPropertyName("registrationId")] string RegistrationId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("submittedAt")] DateTimeOffset SubmittedAt
);

public sealed record AdminSellerRegistrationItemDto(
    [property: JsonPropertyName("registrationId")] string RegistrationId,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("shopName")] string ShopName,
    [property: JsonPropertyName("businessEmail")] string? BusinessEmail,
    [property: JsonPropertyName("hotline")] string? Hotline,
    [property: JsonPropertyName("identityCardNumber")] string? IdentityCardNumber,
    [property: JsonPropertyName("warehouseAddress")] string? WarehouseAddress,
    [property: JsonPropertyName("pickupAddress")] string? PickupAddress,
    [property: JsonPropertyName("reputationScore")] int ReputationScore,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt
);

public sealed record ApproveSellerRegistrationResponseDto(
    [property: JsonPropertyName("registrationId")] string RegistrationId,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("approvedAt")] DateTimeOffset ApprovedAt
);

public sealed record RejectSellerRegistrationResponseDto(
    [property: JsonPropertyName("registrationId")] string RegistrationId,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("rejectedAt")] DateTimeOffset RejectedAt,
    [property: JsonPropertyName("rejectionReason")] string RejectionReason
);
