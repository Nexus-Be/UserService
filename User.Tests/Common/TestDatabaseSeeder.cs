namespace User.Tests.Common;

using System;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Infrastructure.Persistence;

public static class TestDatabaseSeeder
{
    public static (Role Buyer, Role Seller, Role Staff, Role Admin) SeedSystemRoles(AppDbContext dbContext)
    {
        var buyer = new Role
        {
            Id = Guid.NewGuid(),
            RoleCode = RoleType.Buyer,
            RoleName = "Buyer",
            IsActive = true
        };

        var seller = new Role
        {
            Id = Guid.NewGuid(),
            RoleCode = RoleType.Seller,
            RoleName = "Seller",
            IsActive = true,
            ParentRoleId = buyer.Id,
            ParentRole = buyer
        };

        var staff = new Role
        {
            Id = Guid.NewGuid(),
            RoleCode = RoleType.SupportStaff,
            RoleName = "Support Staff",
            IsActive = true
        };

        var admin = new Role
        {
            Id = Guid.NewGuid(),
            RoleCode = RoleType.Admin,
            RoleName = "Administrator",
            IsActive = true,
            ParentRoleId = staff.Id,
            ParentRole = staff
        };

        dbContext.Roles.AddRange(buyer, seller, staff, admin);
        dbContext.SaveChanges();

        return (buyer, seller, staff, admin);
    }
}
