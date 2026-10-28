namespace User.Application.Dtos.Admin;

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
