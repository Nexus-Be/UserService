namespace User.Application.Services.Seller;

using System;
using System.Text.Json.Serialization;

public sealed record SellerRegistrationResponseDto(
    [property: JsonPropertyName("businessEmail")] string BusinessEmail,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("otpExpiresInSeconds")] int OtpExpiresInSeconds,
    [property: JsonPropertyName("nextStep")] string NextStep,
    [property: JsonPropertyName("submittedAt")] DateTimeOffset SubmittedAt
);

public sealed record VerifySellerOtpResponseDto(
    [property: JsonPropertyName("sellerId")] string SellerId,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("businessEmail")] string BusinessEmail,
    [property: JsonPropertyName("shopName")] string ShopName,
    [property: JsonPropertyName("verifiedAt")] DateTimeOffset VerifiedAt,
    [property: JsonPropertyName("message")] string Message
);

public sealed record SellerRegistrationStatusResponseDto(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("rejectionReason")] string? RejectionReason = null,
    [property: JsonPropertyName("nextStep")] string? NextStep = null,
    [property: JsonPropertyName("businessEmail")] string? BusinessEmail = null
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

public sealed record PublicSellerProfileResponseDto(
    [property: JsonPropertyName("shopId")] string ShopId,
    [property: JsonPropertyName("sellerId")] string SellerId,
    [property: JsonPropertyName("shopName")] string ShopName,
    [property: JsonPropertyName("logoUrl")] string? LogoUrl,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("hotline")] string Hotline,
    [property: JsonPropertyName("ratingAvg")] double RatingAvg,
    [property: JsonPropertyName("totalProducts")] int TotalProducts,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt
);

public sealed record SellerRegistrationDetailsDto(
    [property: JsonPropertyName("registrationId")] string RegistrationId,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("applicantName")] string ApplicantName,
    [property: JsonPropertyName("applicantEmail")] string ApplicantEmail,
    [property: JsonPropertyName("applicantPhone")] string? ApplicantPhone,
    [property: JsonPropertyName("shopName")] string ShopName,
    [property: JsonPropertyName("avatarUrl")] string? AvatarUrl,
    [property: JsonPropertyName("businessEmail")] string? BusinessEmail,
    [property: JsonPropertyName("hotline")] string Hotline,
    [property: JsonPropertyName("identityCardNumber")] string? IdentityCardNumber,
    [property: JsonPropertyName("idCardFrontUrl")] string? IdCardFrontUrl,
    [property: JsonPropertyName("idCardBackUrl")] string? IdCardBackUrl,
    [property: JsonPropertyName("businessLicenseUrl")] string? BusinessLicenseUrl,
    [property: JsonPropertyName("pickupAddress")] string PickupAddress,
    [property: JsonPropertyName("warehouseAddress")] string WarehouseAddress,
    [property: JsonPropertyName("provinceId")] int ProvinceId,
    [property: JsonPropertyName("districtId")] int DistrictId,
    [property: JsonPropertyName("wardCode")] string WardCode,
    [property: JsonPropertyName("reputationScore")] int ReputationScore,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("rejectionReason")] string? RejectionReason,
    [property: JsonPropertyName("reviewedBy")] string? ReviewedBy,
    [property: JsonPropertyName("reviewedAt")] DateTimeOffset? ReviewedAt,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset? UpdatedAt
);

public sealed record SellerBankInfoDto(
    [property: JsonPropertyName("bankName")] string? BankName = null,
    [property: JsonPropertyName("bankAccountNumber")] string? BankAccountNumber = null,
    [property: JsonPropertyName("bankAccountHolder")] string? BankAccountHolder = null,
    [property: JsonPropertyName("bankBranch")] string? BankBranch = null
);

public sealed record SellerSettlementInfoDto(
    [property: JsonPropertyName("settlementCycle")] string SettlementCycle = "Weekly",
    [property: JsonPropertyName("autoPayoutEnabled")] bool AutoPayoutEnabled = true,
    [property: JsonPropertyName("payoutStatus")] string PayoutStatus = "Active"
);

public sealed record AdminSellerProfileResponseDto(
    [property: JsonPropertyName("sellerId")] string SellerId,
    [property: JsonPropertyName("shopId")] string ShopId,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("shopName")] string ShopName,
    [property: JsonPropertyName("avatarUrl")] string? AvatarUrl,
    [property: JsonPropertyName("businessEmail")] string? BusinessEmail,
    [property: JsonPropertyName("hotline")] string Hotline,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("pickupAddress")] string PickupAddress,
    [property: JsonPropertyName("warehouseAddress")] string WarehouseAddress,
    [property: JsonPropertyName("provinceId")] int ProvinceId,
    [property: JsonPropertyName("districtId")] int DistrictId,
    [property: JsonPropertyName("wardCode")] string WardCode,
    [property: JsonPropertyName("identityCardNumber")] string? IdentityCardNumber,
    [property: JsonPropertyName("idCardFrontUrl")] string? IdCardFrontUrl,
    [property: JsonPropertyName("idCardBackUrl")] string? IdCardBackUrl,
    [property: JsonPropertyName("businessLicenseUrl")] string? BusinessLicenseUrl,
    [property: JsonPropertyName("ownerFullName")] string OwnerFullName,
    [property: JsonPropertyName("ownerEmail")] string OwnerEmail,
    [property: JsonPropertyName("ownerPhoneNumber")] string? OwnerPhoneNumber,
    [property: JsonPropertyName("ownerGender")] string OwnerGender,
    [property: JsonPropertyName("ownerDateOfBirth")] DateTimeOffset? OwnerDateOfBirth,
    [property: JsonPropertyName("ownerAddress")] string? OwnerAddress,
    [property: JsonPropertyName("bankInfo")] SellerBankInfoDto BankInfo,
    [property: JsonPropertyName("settlementInfo")] SellerSettlementInfoDto SettlementInfo,
    [property: JsonPropertyName("ratingAvg")] double RatingAvg,
    [property: JsonPropertyName("reputationScore")] int ReputationScore,
    [property: JsonPropertyName("trustLevel")] string TrustLevel,
    [property: JsonPropertyName("totalPositiveReviews")] int TotalPositiveReviews,
    [property: JsonPropertyName("totalNegativeReviews")] int TotalNegativeReviews,
    [property: JsonPropertyName("penaltyCount")] int PenaltyCount,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("rejectionReason")] string? RejectionReason,
    [property: JsonPropertyName("reviewedBy")] string? ReviewedBy,
    [property: JsonPropertyName("reviewedByName")] string? ReviewedByName,
    [property: JsonPropertyName("reviewedAt")] DateTimeOffset? ReviewedAt,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset? UpdatedAt,
    [property: JsonPropertyName("sellerReputationScore")] int SellerReputationScore = 50,
    [property: JsonPropertyName("sellerTrustLevel")] string SellerTrustLevel = "Standard"
);

public sealed record SuspendSellerResponseDto(
    [property: JsonPropertyName("sellerId")] string SellerId,
    [property: JsonPropertyName("shopName")] string ShopName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("suspendedAt")] DateTimeOffset SuspendedAt,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("reason")] string Reason
);

public sealed record ReactivateSellerResponseDto(
    [property: JsonPropertyName("sellerId")] string SellerId,
    [property: JsonPropertyName("shopName")] string ShopName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("reactivatedAt")] DateTimeOffset ReactivatedAt,
    [property: JsonPropertyName("reason")] string Reason
);