namespace User.Application.Common.Extensions;

using System.Collections.Generic;
using User.Domain.Entities;
using User.Domain.Enums;

public static class RoleExtensions
{
    /// <summary>
    /// Traverses the Role Hierarchy (via ParentRole) and returns standardized role claims (e.g. ["seller", "buyer"], ["admin", "staff"]).
    /// </summary>
    public static List<string> GetInheritedRoleClaims(this User? user)
    {
        return user?.Role.GetInheritedRoleClaims() ?? new List<string> { "buyer" };
    }

    /// <summary>
    /// Traverses the Role Hierarchy (via ParentRole) and returns standardized role claims.
    /// </summary>
    public static List<string> GetInheritedRoleClaims(this Role? role)
    {
        var roles = new List<string>();
        var currentRole = role;

        while (currentRole != null)
        {
            var roleClaim = currentRole.RoleCode switch
            {
                RoleType.Admin => "admin",
                RoleType.SupportStaff => "staff",
                RoleType.Seller => "seller",
                RoleType.Buyer => "buyer",
                _ => currentRole.RoleCode.ToString().ToLowerInvariant()
            };

            if (!roles.Contains(roleClaim))
            {
                roles.Add(roleClaim);
            }

            var roleName = currentRole.RoleName?.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(roleName) && !roles.Contains(roleName))
            {
                roles.Add(roleName);
            }

            currentRole = currentRole.ParentRole;
        }

        if (roles.Count == 0)
        {
            roles.Add("buyer");
        }

        return roles;
    }
}
