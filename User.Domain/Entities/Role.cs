using System;
using System.Collections.Generic;
using User.Domain.Base;
using User.Domain.Enums;

namespace User.Domain.Entities;

public class Role : AuditableEntity<Guid>
{
    public required RoleType RoleCode { get; set; }
    public required string RoleName { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemRole { get; set; } = false;

    // Role Hierarchy (Self-referencing Single Inheritance)
    public Guid? ParentRoleId { get; set; }
    public Role? ParentRole { get; set; }
    public ICollection<Role> SubRoles { get; set; } = new List<Role>();

    // Navigation
    public ICollection<User> Users { get; set; } = new List<User>();

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
