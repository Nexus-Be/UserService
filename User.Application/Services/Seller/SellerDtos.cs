
using System.Text.Json.Serialization;

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
