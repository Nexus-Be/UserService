namespace User.Application.Services.AdminConfig;

using System;

public sealed record SystemConfigItemDto
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string DataType { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }
}
