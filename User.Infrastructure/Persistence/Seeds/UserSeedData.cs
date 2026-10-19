namespace User.Infrastructure.Persistence.Seeds;

using Microsoft.EntityFrameworkCore;
using User.Domain.Entities;
using User.Domain.Enums;

public static class UserSeedData
{
    public static void SeedRolesAndConfigs(ModelBuilder modelBuilder)
    {
        // Default System Roles
        var buyerRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var sellerRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var supportRoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var adminRoleId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        modelBuilder.Entity<Role>().HasData(
            new Role
            {
                Id = buyerRoleId,
                RoleCode = RoleType.Buyer,
                RoleName = "Buyer",
                Description = "Khách mua hàng và tham gia đấu giá",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                IsDeleted = false
            },
            new Role
            {
                Id = sellerRoleId,
                RoleCode = RoleType.Seller,
                RoleName = "Seller",
                Description = "Người bán hàng và mở phiên đấu giá",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                IsDeleted = false
            },
            new Role
            {
                Id = supportRoleId,
                RoleCode = RoleType.SupportStaff,
                RoleName = "Support Staff",
                Description = "Nhân viên hỗ trợ thẩm định và chăm sóc khách hàng",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                IsDeleted = false
            },
            new Role
            {
                Id = adminRoleId,
                RoleCode = RoleType.Admin,
                RoleName = "Administrator",
                Description = "Quản trị viên tối cao sàn",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                IsDeleted = false
            }
        );
    }
}
