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

    [JsonPropertyName("id_card_front_url")]
    public string? IdCardFrontUrlSnake { set => IdCardFrontUrl ??= value; }

    [JsonPropertyName("idCardFrontUrl")]
    public string? IdCardFrontUrl { get; set; }

    [JsonPropertyName("id_card_back_url")]
    public string? IdCardBackUrlSnake { set => IdCardBackUrl ??= value; }

    [JsonPropertyName("idCardBackUrl")]
    public string? IdCardBackUrl { get; set; }

    [JsonPropertyName("business_license_url")]
    public string? BusinessLicenseUrlSnake { set => BusinessLicenseUrl ??= value; }

    [JsonPropertyName("businessLicenseUrl")]
    public string? BusinessLicenseUrl { get; set; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrlSnake { set => AvatarUrl ??= value; }

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("warehouseAddress")]
    public WarehouseAddressDto? WarehouseAddress { get; set; }

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

public sealed class VerifySellerOtpDto
{
    [JsonPropertyName("business_email")]
    public string? BusinessEmailSnake { set { if (value != null) BusinessEmail = value; } }

    [JsonPropertyName("businessEmail")]
    public string BusinessEmail { get; set; } = string.Empty;

    [JsonPropertyName("otp_code")]
    public string? OtpCodeSnake { set { if (value != null) OtpCode = value; } }

    [JsonPropertyName("otpCode")]
    public string? OtpCodeCamel { set { if (value != null) OtpCode = value; } }

    [JsonPropertyName("otp")]
    public string OtpCode { get; set; } = string.Empty;
}

public sealed class ResendSellerOtpDto
{
    [JsonPropertyName("business_email")]
    public string? BusinessEmailSnake { set { if (value != null) BusinessEmail = value; } }

    [JsonPropertyName("businessEmail")]
    public string BusinessEmail { get; set; } = string.Empty;
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

public sealed record SellerRegistrationDraft(
    Guid UserId,
    string ShopName,
    string BusinessEmail,
    string Hotline,
    string IdentityCardNumber,
    string PickupAddress,
    int ProvinceId,
    int DistrictId,
    string WardCode,
    string? IdCardFrontUrl,
    string? IdCardBackUrl,
    string? BusinessLicenseUrl,
    string? AvatarUrl
);
