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
