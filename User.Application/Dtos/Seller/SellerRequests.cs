namespace User.Application.Dtos.Seller;

using System.Text.Json.Serialization;

public sealed class CreateSellerRegistrationDto
{
    [JsonPropertyName("shopName")]
    public string? ShopName { get; set; }

    [JsonPropertyName("businessEmail")]
    public string? BusinessEmail { get; set; }

    [JsonPropertyName("hotline")]
    public string? Hotline { get; set; }

    [JsonPropertyName("identityCardNumber")]
    public string? IdentityCardNumber { get; set; }

    [JsonPropertyName("warehouseAddress")]
    public WarehouseAddressDto? WarehouseAddress { get; set; }

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
}

public sealed class WarehouseAddressDto
{
    [JsonPropertyName("provinceId")]
    public int? ProvinceId { get; set; }

    [JsonPropertyName("districtId")]
    public int? DistrictId { get; set; }

    [JsonPropertyName("wardCode")]
    public string? WardCode { get; set; }

    [JsonPropertyName("streetAddress")]
    public string? StreetAddress { get; set; }
}

public sealed class ApproveSellerRegistrationDto
{
    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

public sealed class RejectSellerRegistrationDto
{
    [JsonPropertyName("rejection_reason")]
    public string? RejectionReasonSnake { set => RejectionReason ??= value; }

    [JsonPropertyName("rejectionReason")]
    public string? RejectionReason { get; set; }
}
