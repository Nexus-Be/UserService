namespace User.Application.Services.Admin;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public sealed class CreateAdminUserDto
{
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstNameSnake { set => FirstName ??= value; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastNameSnake { set => LastName ??= value; }

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("phone_number")]
    public string? PhoneNumberSnake { set => PhoneNumber ??= value; }

    [JsonPropertyName("phone")]
    public string? PhoneShort { set => PhoneNumber ??= value; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("roleCodes")]
    public List<string>? RoleCodes { get; set; }

    [JsonPropertyName("role_codes")]
    public List<string>? RoleCodesSnake { set => RoleCodes ??= value; }

    [JsonPropertyName("roles")]
    public List<string>? Roles { set => RoleCodes ??= value; }

    [JsonPropertyName("role")]
    public string? SingleRole
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                RoleCodes ??= new List<string>();
                RoleCodes.Add(value);
            }
        }
    }
}

public sealed record AdminUserCreatedResponseDto(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("roles")] List<string> Roles,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt
);

public sealed record AdminUserListItemDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("firstName")] string FirstName,
    [property: JsonPropertyName("lastName")] string LastName,
    [property: JsonPropertyName("phoneNumber")] string? PhoneNumber,
    [property: JsonPropertyName("avatarUrl")] string? AvatarUrl,
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("reputationScore")] int ReputationScore,
    [property: JsonPropertyName("trustLevel")] string TrustLevel,
    [property: JsonPropertyName("isSeller")] bool IsSeller,
    [property: JsonPropertyName("roles")] List<string> Roles,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt
);

public sealed record AdminUserListPagedResultDto(
    [property: JsonPropertyName("items")] IReadOnlyList<AdminUserListItemDto> Items,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("limit")] int Limit
);

public sealed record AdminSellerProfileDetailDto(
    [property: JsonPropertyName("shopName")] string ShopName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("hotline")] string? Hotline,
    [property: JsonPropertyName("businessEmail")] string? BusinessEmail,
    [property: JsonPropertyName("pickupAddress")] string? PickupAddress,
    [property: JsonPropertyName("provinceId")] int? ProvinceId,
    [property: JsonPropertyName("districtId")] int? DistrictId,
    [property: JsonPropertyName("wardCode")] string? WardCode,
    [property: JsonPropertyName("avatarUrl")] string? AvatarUrl,
    [property: JsonPropertyName("idCardFrontUrl")] string? IdCardFrontUrl,
    [property: JsonPropertyName("idCardBackUrl")] string? IdCardBackUrl,
    [property: JsonPropertyName("businessLicenseUrl")] string? BusinessLicenseUrl,
    [property: JsonPropertyName("rejectionReason")] string? RejectionReason,
    [property: JsonPropertyName("reviewedAt")] DateTimeOffset? ReviewedAt
);

public sealed record AdminUserReputationLogDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("changeAmount")] int ChangeAmount,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("referenceType")] string? ReferenceType,
    [property: JsonPropertyName("referenceId")] string? ReferenceId,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt
);

public sealed record AdminUserDetailDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("firstName")] string FirstName,
    [property: JsonPropertyName("lastName")] string LastName,
    [property: JsonPropertyName("phoneNumber")] string? PhoneNumber,
    [property: JsonPropertyName("identifyNumber")] string? IdentifyNumber,
    [property: JsonPropertyName("avatarUrl")] string? AvatarUrl,
    [property: JsonPropertyName("gender")] string Gender,
    [property: JsonPropertyName("dateOfBirth")] DateTimeOffset? DateOfBirth,
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("isSeller")] bool IsSeller,
    [property: JsonPropertyName("reputationScore")] int ReputationScore,
    [property: JsonPropertyName("trustLevel")] string TrustLevel,
    [property: JsonPropertyName("roles")] List<string> Roles,
    [property: JsonPropertyName("permissions")] List<string> Permissions,
    [property: JsonPropertyName("sellerProfile")] AdminSellerProfileDetailDto? SellerProfile,
    [property: JsonPropertyName("reputationLogs")] List<AdminUserReputationLogDto> ReputationLogs,
    [property: JsonPropertyName("bannedAt")] DateTimeOffset? BannedAt,
    [property: JsonPropertyName("banReason")] string? BanReason,
    [property: JsonPropertyName("emailVerifiedAt")] DateTimeOffset? EmailVerifiedAt,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset? UpdatedAt
);

public sealed record AdminRoleDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("roleId")] string RoleId,
    [property: JsonPropertyName("roleCode")] string RoleCode,
    [property: JsonPropertyName("roleName")] string RoleName,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("isSystemRole")] bool IsSystemRole,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("permissionsCount")] int PermissionsCount,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt
);

public sealed record AdminRoleListResponseDto(
    [property: JsonPropertyName("roles")] List<AdminRoleDto> Roles
);

public sealed class AssignAdminUserRolesDto
{
    [JsonPropertyName("roleCodes")]
    public List<string>? RoleCodes { get; set; }

    [JsonPropertyName("role_codes")]
    public List<string>? RoleCodesSnake { set => RoleCodes ??= value; }

    [JsonPropertyName("roles")]
    public List<string>? Roles { set => RoleCodes ??= value; }

    [JsonPropertyName("roleIds")]
    public List<string>? RoleIds { get; set; }

    [JsonPropertyName("role_ids")]
    public List<string>? RoleIdsSnake { set => RoleIds ??= value; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

public sealed record AdminUserRolesResponseDto(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("roles")] List<string> Roles,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt
);

