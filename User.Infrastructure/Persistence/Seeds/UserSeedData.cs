namespace User.Infrastructure.Persistence.Seeds;

using Microsoft.EntityFrameworkCore;
using User.Domain.Entities;
using User.Domain.Enums;

public static class UserSeedData
{
    public static void SeedRolesAndConfigs(ModelBuilder modelBuilder)
    {
        var seedDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        const string defaultPasswordHash = "$2a$11$BbBEMUrvmyRMgcL9IgBuzugG2R8p/9h0GXmTMGwy1iiBy/Yl6hsuG"; // "Password123!"

        // 1. Roles (4 System Roles)
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
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new Role
            {
                Id = sellerRoleId,
                RoleCode = RoleType.Seller,
                RoleName = "Seller",
                Description = "Người bán hàng và mở phiên đấu giá",
                IsActive = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new Role
            {
                Id = supportRoleId,
                RoleCode = RoleType.SupportStaff,
                RoleName = "Support Staff",
                Description = "Nhân viên hỗ trợ thẩm định và chăm sóc khách hàng",
                IsActive = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new Role
            {
                Id = adminRoleId,
                RoleCode = RoleType.Admin,
                RoleName = "Administrator",
                Description = "Quản trị viên tối cao sàn",
                IsActive = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );

        // 2. Users (4 Users)
        var adminUserId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
        var staffUserId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
        var sellerUserId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");
        var buyerUserId = Guid.Parse("dddddddd-4444-4444-4444-444444444444");

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = adminUserId,
                Email = "admin@nexus.com",
                PasswordHash = defaultPasswordHash,
                FirstName = "Admin",
                LastName = "System",
                PhoneNumber = "0900000001",
                Status = UserStatus.Active,
                Gender = Gender.Unspecified,
                EmailVerifiedAt = seedDate,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new User
            {
                Id = staffUserId,
                Email = "staff@nexus.com",
                PasswordHash = defaultPasswordHash,
                FirstName = "Staff",
                LastName = "Support",
                PhoneNumber = "0900000002",
                Status = UserStatus.Active,
                Gender = Gender.Unspecified,
                EmailVerifiedAt = seedDate,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new User
            {
                Id = sellerUserId,
                Email = "seller@nexus.com",
                PasswordHash = defaultPasswordHash,
                FirstName = "Official",
                LastName = "Seller",
                PhoneNumber = "0900000003",
                Status = UserStatus.Active,
                Gender = Gender.Unspecified,
                EmailVerifiedAt = seedDate,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new User
            {
                Id = buyerUserId,
                Email = "buyer@nexus.com",
                PasswordHash = defaultPasswordHash,
                FirstName = "John",
                LastName = "Buyer",
                PhoneNumber = "0900000004",
                Address = "123 Nguyen Trai, Thanh Xuan, Ha Noi",
                Status = UserStatus.Active,
                Gender = Gender.Unspecified,
                EmailVerifiedAt = seedDate,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );

        // 3. User Roles (Mapping 4 Users to 4 Roles)
        modelBuilder.Entity<UserRole>().HasData(
            new UserRole
            {
                Id = Guid.Parse("faaaaaaa-1111-1111-1111-111111111111"),
                UserId = adminUserId,
                RoleId = adminRoleId,
                AssignedAt = seedDate,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new UserRole
            {
                Id = Guid.Parse("fbbbbbbb-2222-2222-2222-222222222222"),
                UserId = staffUserId,
                RoleId = supportRoleId,
                AssignedAt = seedDate,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new UserRole
            {
                Id = Guid.Parse("fccccccc-3333-3333-3333-333333333333"),
                UserId = sellerUserId,
                RoleId = sellerRoleId,
                AssignedAt = seedDate,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new UserRole
            {
                Id = Guid.Parse("fddddddd-4444-4444-4444-444444444444"),
                UserId = buyerUserId,
                RoleId = buyerRoleId,
                AssignedAt = seedDate,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );

        // 4. SellerProfile for Seller User
        modelBuilder.Entity<SellerProfile>().HasData(
            new SellerProfile
            {
                Id = Guid.Parse("cccccccc-3333-3333-3333-333333333334"),
                UserId = sellerUserId,
                ShopName = "Nexus Official Store",
                BusinessEmail = "seller@nexus.com",
                PickupAddress = "Toa nha Nexus, Duy Tan, Cau Giay, Ha Noi",
                ProvinceId = 1,
                DistrictId = 1,
                WardCode = "00001",
                Hotline = "19001234",
                Status = SellerProfileStatus.Approved,
                ReviewedBy = adminUserId,
                ReviewedAt = seedDate,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );

        // 5. ReputationScores
        modelBuilder.Entity<ReputationScore>().HasData(
            new ReputationScore
            {
                Id = Guid.Parse("eaaaaaaa-1111-1111-1111-111111111111"),
                UserId = adminUserId,
                CurrentScore = 100,
                TrustLevel = TrustLevel.Excellent,
                TotalPositiveReviews = 0,
                TotalNegativeReviews = 0,
                PenaltyCount = 0,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new ReputationScore
            {
                Id = Guid.Parse("ebbbbbbb-2222-2222-2222-222222222222"),
                UserId = staffUserId,
                CurrentScore = 100,
                TrustLevel = TrustLevel.Excellent,
                TotalPositiveReviews = 0,
                TotalNegativeReviews = 0,
                PenaltyCount = 0,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new ReputationScore
            {
                Id = Guid.Parse("eccccccc-3333-3333-3333-333333333333"),
                UserId = sellerUserId,
                CurrentScore = 100,
                TrustLevel = TrustLevel.Good,
                TotalPositiveReviews = 50,
                TotalNegativeReviews = 0,
                PenaltyCount = 0,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new ReputationScore
            {
                Id = Guid.Parse("eddddddd-4444-4444-4444-444444444444"),
                UserId = buyerUserId,
                CurrentScore = 80,
                TrustLevel = TrustLevel.Standard,
                TotalPositiveReviews = 10,
                TotalNegativeReviews = 0,
                PenaltyCount = 0,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );

        // 6. System Configurations
        modelBuilder.Entity<SystemConfig>().HasData(
            new SystemConfig
            {
                Id = Guid.Parse("11111111-2222-3333-4444-555555555551"),
                ConfigKey = "DEFAULT_REPUTATION_SCORE",
                ConfigValue = "50",
                DataType = ConfigDataType.Int,
                Description = "Điểm uy tín mặc định khi đăng ký mới",
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new SystemConfig
            {
                Id = Guid.Parse("11111111-2222-3333-4444-555555555552"),
                ConfigKey = "AUTO_APPROVE_BUYER",
                ConfigValue = "true",
                DataType = ConfigDataType.Boolean,
                Description = "Tự động kích hoạt tài khoản buyer sau xác thực email",
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );
    }
}
