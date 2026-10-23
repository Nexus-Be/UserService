namespace User.Application.Models;

using System;

public enum OtpStatus
{
    Active = 0,
    Consumed = 1,
    Expired = 2
}

public sealed class OtpEntry
{
    public string Code { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public OtpStatus Status { get; set; } = OtpStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}
