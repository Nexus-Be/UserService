using System.ComponentModel.DataAnnotations.Schema;
using User.Domain.Base;

namespace User.Domain.Entities;

public class RefreshToken : BaseEntity<Guid>
{
    public required Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public string? DeviceInfo { get; set; }
    public string? IpAddress { get; set; }
    public bool IsRevoked { get; set; } = false;

    [NotMapped]
    public bool IsDisabled
    {
        get => IsRevoked;
        set => IsRevoked = value;
    }

    public required DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    // Navigation
    public User? User { get; set; }
}
