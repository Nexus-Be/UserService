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
                Username = "admin",
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
                Username = "staff",
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
                Username = "seller",
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
                Username = "buyer",
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

        // 7. Permissions
        var permUserReadId = Guid.Parse("10000000-aaaa-bbbb-cccc-000000000001");
        var permUserWriteId = Guid.Parse("10000000-aaaa-bbbb-cccc-000000000002");
        var permSellerApproveId = Guid.Parse("10000000-aaaa-bbbb-cccc-000000000003");
        var permAuctionManageId = Guid.Parse("10000000-aaaa-bbbb-cccc-000000000004");

        modelBuilder.Entity<Permission>().HasData(
            new Permission
            {
                Id = permUserReadId,
                PermissionCode = "PERM_USER_READ",
                PermissionName = "Xem Danh Sách & Hồ Sơ Người Dùng",
                Category = "User",
                Description = "Quyền truy cập thông tin người dùng",
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new Permission
            {
                Id = permUserWriteId,
                PermissionCode = "PERM_USER_WRITE",
                PermissionName = "Chỉnh Sửa Thông Tin Người Dùng",
                Category = "User",
                Description = "Quyền quản trị và cập nhật người dùng",
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new Permission
            {
                Id = permSellerApproveId,
                PermissionCode = "PERM_SELLER_APPROVE",
                PermissionName = "Phê Duyệt Hồ Sơ Người Bán",
                Category = "Seller",
                Description = "Quyền xét duyệt đăng ký bán hàng",
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new Permission
            {
                Id = permAuctionManageId,
                PermissionCode = "PERM_AUCTION_MANAGE",
                PermissionName = "Quản Trị Phiên Đấu Giá",
                Category = "Auction",
                Description = "Quyền kiểm duyệt và điều hành đấu giá",
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );

        // 8. Role Permissions
        modelBuilder.Entity<RolePermission>().HasData(
            new RolePermission
            {
                Id = Guid.Parse("20000000-aaaa-bbbb-cccc-000000000001"),
                RoleId = adminRoleId,
                PermissionId = permUserReadId,
                AssignedAt = seedDate,
                AssignedBy = adminUserId,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new RolePermission
            {
                Id = Guid.Parse("20000000-aaaa-bbbb-cccc-000000000002"),
                RoleId = adminRoleId,
                PermissionId = permUserWriteId,
                AssignedAt = seedDate,
                AssignedBy = adminUserId,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new RolePermission
            {
                Id = Guid.Parse("20000000-aaaa-bbbb-cccc-000000000003"),
                RoleId = adminRoleId,
                PermissionId = permSellerApproveId,
                AssignedAt = seedDate,
                AssignedBy = adminUserId,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new RolePermission
            {
                Id = Guid.Parse("20000000-aaaa-bbbb-cccc-000000000004"),
                RoleId = adminRoleId,
                PermissionId = permAuctionManageId,
                AssignedAt = seedDate,
                AssignedBy = adminUserId,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new RolePermission
            {
                Id = Guid.Parse("20000000-aaaa-bbbb-cccc-000000000005"),
                RoleId = supportRoleId,
                PermissionId = permUserReadId,
                AssignedAt = seedDate,
                AssignedBy = adminUserId,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new RolePermission
            {
                Id = Guid.Parse("20000000-aaaa-bbbb-cccc-000000000006"),
                RoleId = supportRoleId,
                PermissionId = permSellerApproveId,
                AssignedAt = seedDate,
                AssignedBy = adminUserId,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );

        // 9. Reputation Logs
        modelBuilder.Entity<ReputationLog>().HasData(
            new ReputationLog
            {
                Id = Guid.Parse("30000000-aaaa-bbbb-cccc-000000000001"),
                UserId = sellerUserId,
                ChangeAmount = 50,
                Reason = ReputationChangeReason.OrderCompleted,
                ReferenceType = ReferenceType.Order,
                ReferenceId = "02000000-0000-0000-0000-000000000001",
                CreatedBy = adminUserId,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new ReputationLog
            {
                Id = Guid.Parse("30000000-aaaa-bbbb-cccc-000000000002"),
                UserId = buyerUserId,
                ChangeAmount = 10,
                Reason = ReputationChangeReason.PositiveReview,
                ReferenceType = ReferenceType.Order,
                ReferenceId = "02000000-0000-0000-0000-000000000001",
                CreatedBy = sellerUserId,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );

        // 10. User Reviews
        modelBuilder.Entity<UserReview>().HasData(
            new UserReview
            {
                Id = Guid.Parse("40000000-aaaa-bbbb-cccc-000000000001"),
                ReviewerId = buyerUserId,
                RevieweeId = sellerUserId,
                ReviewerRole = ReviewerRole.Buyer,
                ReferenceType = ReferenceType.Order,
                ReferenceId = "02000000-0000-0000-0000-000000000001",
                Rating = 5,
                Comment = "Người bán phản hồi nhanh, hàng chuẩn chính hãng đóng gói kỹ lưỡng.",
                MediaUrls = new List<string> { "https://images.nexus.local/reviews/user-review-01.png" },
                IsDisputed = false,
                IsDisabled = false,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );

        // 11. User Reports
        modelBuilder.Entity<UserReport>().HasData(
            new UserReport
            {
                Id = Guid.Parse("50000000-aaaa-bbbb-cccc-000000000001"),
                ReporterId = buyerUserId,
                ReportedUserId = sellerUserId,
                ViolationType = ReportViolationType.Other,
                Description = "Yêu cầu cung cấp thêm hoá đơn VAT cho đơn hàng",
                EvidenceUrls = new List<string> { "https://images.nexus.local/reports/invoice-request.png" },
                Status = ReportStatus.Resolved,
                ResolvedBy = staffUserId,
                ResolutionNote = "Người bán đã xuất hoá đơn điện tử gửi qua email.",
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );

        // 12. User Penalties
        modelBuilder.Entity<UserPenalty>().HasData(
            new UserPenalty
            {
                Id = Guid.Parse("60000000-aaaa-bbbb-cccc-000000000001"),
                UserId = buyerUserId,
                ViolationType = PenaltyViolationType.PaymentTimeout,
                Reason = "Quá hạn thanh toán thử nghiệm trong môi trường sandbox",
                PenaltyApplied = PenaltyType.Warning,
                PointsDeducted = 5,
                Status = PenaltyStatus.Expired,
                IssuedAt = seedDate,
                ExpiresAt = seedDate.AddDays(30),
                CreatedBy = adminUserId,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );

        // 13. Audit Logs
        modelBuilder.Entity<AuditLog>().HasData(
            new AuditLog
            {
                Id = Guid.Parse("70000000-aaaa-bbbb-cccc-000000000001"),
                ServiceName = "User",
                TraceId = "trace-seed-user-001",
                UserId = adminUserId.ToString(),
                UserName = "admin@nexus.com",
                Action = AuditAction.SellerApprove,
                EntityName = "SellerProfile",
                EntityId = "cccccccc-3333-3333-3333-333333333334",
                OldValues = "{\"Status\": \"Pending\"}",
                NewValues = "{\"Status\": \"Approved\"}",
                ChangedColumns = "[\"Status\"]",
                IpAddress = "127.0.0.1",
                UserAgent = "NexusAdminPortal/1.0",
                CreatedAt = seedDate,
                IsSynced = true
            }
        );
    }
}
