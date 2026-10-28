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

