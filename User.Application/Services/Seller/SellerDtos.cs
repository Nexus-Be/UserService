namespace User.Application.Services.Seller;

using System;
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
    [JsonPropertyName("shopName")]
    public string? ShopName { get; set; }

    [JsonPropertyName("hotline")]
    public string? Hotline { get; set; }

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("businessEmail")]
    public string? BusinessEmail { get; set; }

    [JsonPropertyName("pickupAddress")]
    public string? PickupAddress { get; set; }

    [JsonPropertyName("streetAddress")]
    public string? StreetAddress { get; set; }

    [JsonPropertyName("provinceId")]
    public int? ProvinceId { get; set; }

    [JsonPropertyName("districtId")]
    public int? DistrictId { get; set; }

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
