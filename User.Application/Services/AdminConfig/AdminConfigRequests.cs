namespace User.Application.Services.AdminConfig;

using System.ComponentModel.DataAnnotations;

public sealed record UpdateSystemConfigRequest
{
    [Required(ErrorMessage = "Configuration key is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Configuration key must be between 2 and 100 characters long.")]
    public string Key { get; init; } = string.Empty;

    [Required(ErrorMessage = "Configuration value is required.")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Configuration value must be between 1 and 1000 characters long.")]
    public string Value { get; init; } = string.Empty;

    [StringLength(500, ErrorMessage = "Reason must not exceed 500 characters long.")]
    public string? Reason { get; init; }
}
