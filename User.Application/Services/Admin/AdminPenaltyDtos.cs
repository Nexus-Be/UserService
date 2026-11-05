namespace User.Application.Services.Admin;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using User.Domain.Enums;

public sealed record AdminPenaltyListItemDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("userEmail")] string? UserEmail,
    [property: JsonPropertyName("targetRole")] TargetRole TargetRole,
    [property: JsonPropertyName("penaltyApplied")] PenaltyType PenaltyApplied,
    [property: JsonPropertyName("violationType")] PenaltyViolationType ViolationType,
    [property: JsonPropertyName("status")] PenaltyStatus Status,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("policyClauseCode")] string? PolicyClauseCode,
    [property: JsonPropertyName("pointsDeducted")] int PointsDeducted,
    [property: JsonPropertyName("issuedAt")] DateTimeOffset IssuedAt,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset? ExpiresAt
);

public sealed record AdminPenaltyDetailDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("userEmail")] string? UserEmail,
    [property: JsonPropertyName("userFullName")] string? UserFullName,
    [property: JsonPropertyName("targetRole")] TargetRole TargetRole,
    [property: JsonPropertyName("penaltyApplied")] PenaltyType PenaltyApplied,
    [property: JsonPropertyName("violationType")] PenaltyViolationType ViolationType,
    [property: JsonPropertyName("status")] PenaltyStatus Status,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("policyClauseCode")] string? PolicyClauseCode,
    [property: JsonPropertyName("evidenceUrls")] List<string> EvidenceUrls,
    [property: JsonPropertyName("pointsDeducted")] int PointsDeducted,
    [property: JsonPropertyName("issuedAt")] DateTimeOffset IssuedAt,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("createdBy")] string? CreatedBy,
    [property: JsonPropertyName("revokedBy")] string? RevokedBy,
    [property: JsonPropertyName("revokedAt")] DateTimeOffset? RevokedAt,
    [property: JsonPropertyName("revokeReason")] string? RevokeReason
);

public sealed class RevokePenaltyDto
{
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public sealed record AdminPenaltyRevokedResponseDto(
    [property: JsonPropertyName("penaltyId")] string PenaltyId,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("status")] PenaltyStatus Status,
    [property: JsonPropertyName("revokedAt")] DateTimeOffset RevokedAt,
    [property: JsonPropertyName("revokeReason")] string RevokeReason,
    [property: JsonPropertyName("restoredOperationalState")] string RestoredOperationalState
);

public sealed record UserActionPermissionCheckResponseDto(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("isAllowed")] bool IsAllowed,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("activeRestrictions")] List<string> ActiveRestrictions
);
