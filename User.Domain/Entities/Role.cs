using User.Domain.Base;
using User.Domain.Enums;

namespace User.Domain.Entities;

public class Role : AuditableEntity<Guid>
{
    public required RoleType RoleCode { get; set; }
    public required string RoleName { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    // Navigation
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
