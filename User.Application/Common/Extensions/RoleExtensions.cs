namespace User.Application.Common.Extensions;

using System;
using System.Collections.Generic;
using User.Domain.Entities;
using User.Domain.Enums;

public static class RoleExtensions
{
    public static string ToStandardRole(this RoleType roleType) => roleType switch
    {
        RoleType.Admin => "admin",
        RoleType.SupportStaff => "staff",
        RoleType.Seller => "seller",
        RoleType.Buyer => "buyer",
        _ => roleType.ToString().ToLowerInvariant()
    };

    public static List<string> ExtractStandardRoles(this User user)
    {
        var roles = new List<string>();
        if (user.UserRoles != null && user.UserRoles.Count > 0)
        {
            foreach (var ur in user.UserRoles)
            {
                if (ur.Role == null || ur.IsDeleted) continue;

                var standard = ur.Role.RoleCode.ToStandardRole();
                if (!roles.Contains(standard))
                {
                    roles.Add(standard);
                }

                var roleName = ur.Role.RoleName?.ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(roleName) && !roles.Contains(roleName))
                {
                    roles.Add(roleName);
                }
            }
        }

        if (roles.Count == 0)
        {
            roles.Add("buyer");
        }

        return roles;
    }
}
