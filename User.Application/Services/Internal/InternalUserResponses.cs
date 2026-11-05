namespace User.Application.Services.Internal;

using System.Text.Json.Serialization;

public sealed record CheckReputationResponse(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("allowed")] bool Allowed,
    [property: JsonPropertyName("isApprovedSeller")] bool IsApprovedSeller,
    [property: JsonPropertyName("userStatus")] string UserStatus,
    [property: JsonPropertyName("failureReason")] string? FailureReason
);

public sealed record VerifyBidEligibilityResponse(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("allowed")] bool Allowed,
    [property: JsonPropertyName("failureReason")] string? FailureReason
);

public sealed record UserPublicInfoDto(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("maskedName")] string MaskedName,
    [property: JsonPropertyName("isDeleted")] bool IsDeleted
);
