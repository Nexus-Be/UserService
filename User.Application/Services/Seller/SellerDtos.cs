namespace User.Application.Services.Seller;

using System;
using System.Text.Json.Serialization;

public sealed class CreateSellerRegistrationDto
{
    [JsonPropertyName("shop_name")]
    public string? ShopNameSnake { set => ShopName ??= value; }

    [JsonPropertyName("shopName")]
    public string? ShopName { get; set; }

    [JsonPropertyName("business_email")]
    public string? BusinessEmailSnake { set => BusinessEmail ??= value; }

    [JsonPropertyName("businessEmail")]
    public string? BusinessEmail { get; set; }

    [JsonPropertyName("hotline")]
    public string? Hotline { get; set; }

    [JsonPropertyName("identity_card_number")]
    public string? IdentityCardNumberSnake { set => IdentityCardNumber ??= value; }

    [JsonPropertyName("identityCardNumber")]
    public string? IdentityCardNumber { get; set; }

    [JsonPropertyName("pickup_address")]
    public string? PickupAddressSnake { set => PickupAddress ??= value; }

    [JsonPropertyName("pickupAddress")]
    public string? PickupAddress { get; set; }

    [JsonPropertyName("street_address")]
    public string? StreetAddressSnake { set => StreetAddress ??= value; }

    [JsonPropertyName("streetAddress")]
    public string? StreetAddress { get; set; }

    [JsonPropertyName("province_id")]
    public int? ProvinceIdSnake { set => ProvinceId ??= value; }

    [JsonPropertyName("provinceId")]
    public int? ProvinceId { get; set; }

    [JsonPropertyName("district_id")]
    public int? DistrictIdSnake { set => DistrictId ??= value; }

    [JsonPropertyName("districtId")]
    public int? DistrictId { get; set; }

    [JsonPropertyName("ward_code")]
    public string? WardCodeSnake { set => WardCode ??= value; }

    [JsonPropertyName("wardCode")]
    public string? WardCode { get; set; }

    [JsonPropertyName("warehouse_address")]
    public WarehouseAddressDto? WarehouseAddressSnake { set => WarehouseAddress ??= value; }

    [JsonPropertyName("warehouseAddress")]
    public WarehouseAddressDto? WarehouseAddress { get; set; }
}

public sealed class WarehouseAddressDto
{
    [JsonPropertyName("province_id")]
    public int? ProvinceIdSnake { set => ProvinceId ??= value; }

    [JsonPropertyName("provinceId")]
    public int? ProvinceId { get; set; }

    [JsonPropertyName("district_id")]
    public int? DistrictIdSnake { set => DistrictId ??= value; }

    [JsonPropertyName("districtId")]
    public int? DistrictId { get; set; }

    [JsonPropertyName("ward_code")]
    public string? WardCodeSnake { set => WardCode ??= value; }

    [JsonPropertyName("wardCode")]
    public string? WardCode { get; set; }

    [JsonPropertyName("street_address")]
    public string? StreetAddressSnake { set => StreetAddress ??= value; }

    [JsonPropertyName("streetAddress")]
    public string? StreetAddress { get; set; }
}

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

public sealed record PagedResultDto<T>(
    [property: JsonPropertyName("items")] System.Collections.Generic.IReadOnlyList<T> Items,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("limit")] int Limit
);

public sealed class ApproveSellerRegistrationDto
{
    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

public sealed record ApproveSellerRegistrationResponseDto(
    [property: JsonPropertyName("registrationId")] string RegistrationId,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("approvedAt")] DateTimeOffset ApprovedAt
);

public sealed class RejectSellerRegistrationDto
{
    [JsonPropertyName("rejection_reason")]
    public string? RejectionReasonSnake { set => RejectionReason ??= value; }

    [JsonPropertyName("rejectionReason")]
    public string? RejectionReason { get; set; }
}

public sealed record RejectSellerRegistrationResponseDto(
    [property: JsonPropertyName("registrationId")] string RegistrationId,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("rejectedAt")] DateTimeOffset RejectedAt,
    [property: JsonPropertyName("rejectionReason")] string RejectionReason
);

public sealed record SellerOwnProfileResponseDto(
    [property: JsonPropertyName("sellerId")] string SellerId,
    [property: JsonPropertyName("shopId")] string ShopId,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("shopName")] string ShopName,
    [property: JsonPropertyName("avatarUrl")] string? AvatarUrl,
    [property: JsonPropertyName("businessEmail")] string? BusinessEmail,
    [property: JsonPropertyName("hotline")] string Hotline,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("pickupAddress")] string PickupAddress,
    [property: JsonPropertyName("provinceId")] int ProvinceId,
    [property: JsonPropertyName("districtId")] int DistrictId,
    [property: JsonPropertyName("wardCode")] string WardCode,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("ratingAvg")] double RatingAvg,
    [property: JsonPropertyName("reputationScore")] int ReputationScore,
    [property: JsonPropertyName("trustLevel")] string? TrustLevel,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset? UpdatedAt
);

public sealed class UpdateSellerOwnProfileDto
{
    [JsonPropertyName("shop_name")]
    public string? ShopNameSnake { set => ShopName ??= value; }

    [JsonPropertyName("shopName")]
    public string? ShopName { get; set; }

    [JsonPropertyName("hotline")]
    public string? Hotline { get; set; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrlSnake { set => AvatarUrl ??= value; }

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("business_email")]
    public string? BusinessEmailSnake { set => BusinessEmail ??= value; }

    [JsonPropertyName("businessEmail")]
    public string? BusinessEmail { get; set; }

    [JsonPropertyName("pickup_address")]
    public string? PickupAddressSnake { set => PickupAddress ??= value; }

    [JsonPropertyName("pickupAddress")]
    public string? PickupAddress { get; set; }

    [JsonPropertyName("street_address")]
    public string? StreetAddressSnake { set => StreetAddress ??= value; }

    [JsonPropertyName("streetAddress")]
    public string? StreetAddress { get; set; }

    [JsonPropertyName("province_id")]
    public int? ProvinceIdSnake { set => ProvinceId ??= value; }

    [JsonPropertyName("provinceId")]
    public int? ProvinceId { get; set; }

    [JsonPropertyName("district_id")]
    public int? DistrictIdSnake { set => DistrictId ??= value; }

    [JsonPropertyName("districtId")]
    public int? DistrictId { get; set; }

    [JsonPropertyName("ward_code")]
    public string? WardCodeSnake { set => WardCode ??= value; }

    [JsonPropertyName("wardCode")]
    public string? WardCode { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed record SellerOwnProfileUpdatedResponseDto(
    [property: JsonPropertyName("shopId")] string ShopId,
    [property: JsonPropertyName("sellerId")] string SellerId,
    [property: JsonPropertyName("shopName")] string ShopName,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("message")] string Message
);
