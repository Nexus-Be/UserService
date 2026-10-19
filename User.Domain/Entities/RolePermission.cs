using User.Domain.Base;

namespace User.Domain.Entities;

public class RolePermission : BaseEntity<Guid>
{
    public required Guid RoleId { get; set; }
    public required Guid PermissionId { get; set; }
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? AssignedBy { get; set; }

    // Navigation
    public Role? Role { get; set; }
    public Permission? Permission { get; set; }
}
