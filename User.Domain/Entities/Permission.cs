using User.Domain.Base;

namespace User.Domain.Entities;

public class Permission : AuditableEntity<Guid>
{
    public required string PermissionCode { get; set; }
    public required string PermissionName { get; set; }
    public required string Category { get; set; }
    public string? Description { get; set; }

    // Navigation
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
