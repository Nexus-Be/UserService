using User.Domain.Base;

namespace User.Domain.Entities;

public class UserRole : BaseEntity<Guid>
{
    public required Guid UserId { get; set; }
    public required Guid RoleId { get; set; }
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? AssignedBy { get; set; }

    // Navigation
    public User? User { get; set; }
    public Role? Role { get; set; }
}
