namespace User.Application.Services.Admin;

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

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("roleCodes")]
    public List<string>? RoleCodes { get; set; }
}

public sealed class AssignAdminUserRolesDto
{
    [JsonPropertyName("roleCodes")]
    public List<string>? RoleCodes { get; set; }

    [JsonPropertyName("roleIds")]
    public List<string>? RoleIds { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

public sealed class BanAdminUserDto
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("policyClauseCode")]
    public string? PolicyClauseCode { get; set; }

    [JsonPropertyName("evidenceUrls")]
    public List<string>? EvidenceUrls { get; set; }
}

public sealed class UnbanAdminUserDto
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
