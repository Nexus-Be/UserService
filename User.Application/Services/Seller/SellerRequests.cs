namespace User.Application.Services.Seller;

using System;
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

    [JsonPropertyName("idCardFrontUrl")]
    public string? IdCardFrontUrl { get; set; }

    [JsonPropertyName("idCardBackUrl")]
    public string? IdCardBackUrl { get; set; }

    [JsonPropertyName("businessLicenseUrl")]
    public string? BusinessLicenseUrl { get; set; }

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }

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

public sealed class VerifySellerOtpDto
{
    [JsonPropertyName("businessEmail")]
    public string BusinessEmail { get; set; } = string.Empty;

    [JsonPropertyName("otpCode")]
    public string OtpCode { get; set; } = string.Empty;
}

public sealed class ResendSellerOtpDto
{
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

public sealed class SuspendSellerDto
{
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("durationDays")]
    public int? DurationDays { get; set; }

    [JsonPropertyName("policyClauseCode")]
    public string? PolicyClauseCode { get; set; }

    [JsonPropertyName("evidenceUrls")]
    public System.Collections.Generic.List<string>? EvidenceUrls { get; set; }
}

public sealed class ReactivateSellerDto
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}