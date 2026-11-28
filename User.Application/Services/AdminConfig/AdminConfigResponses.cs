namespace User.Application.Services.AdminConfig;

using System;

public sealed record UpdateSystemConfigResponse
{
    public string Key { get; init; } = string.Empty;
    public string OldValue { get; init; } = string.Empty;
    public string NewValue { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }
}
