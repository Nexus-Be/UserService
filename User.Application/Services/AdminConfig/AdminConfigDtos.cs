namespace User.Application.Services.AdminConfig;

using System;

public sealed record SystemConfigItemDto
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string DataType { get; init; } = string.Empty;
    public decimal? MinValue { get; init; }
    public decimal? MaxValue { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record SystemConfigHistoryItemDto
{
    public Guid Id { get; init; }
    public string? ChangedBy { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string? Reason { get; init; }
    public DateTimeOffset ChangedAt { get; init; }
}
