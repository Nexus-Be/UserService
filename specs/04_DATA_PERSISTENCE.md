# 04 — MÔ HÌNH THỰC THỂ & CƠ SỞ DỮ LIỆU (USER SERVICE DATA PERSISTENCE - V2)
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — **14/14** entity + **14/14** DbSet + **14/14** bảng `dbml/service/user.dbml` ✅; migration tạo **17 bảng** (14 nghiệp vụ + 3 bảng MassTransit Outbox/Inbox). Nhưng chỉ **4/14 DbSet** được code nghiệp vụ đọc/ghi; **10/14 DbSet** chưa có truy vấn nào. **Mọi khóa chính là `Guid`** (không phải `int identity` như đặc tả V2 cũ).
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
---
## 1. MÔ HÌNH THỰC THỂ CHÍNH (DOMAIN ENTITIES)
### 1.0 Bảng tổng hợp trạng thái 14 thực thể
| # | Entity | DbSet | Bảng DB | Configuration | Có code đọc/ghi? | Trạng thái |
|--:|:---|:---|:---|:---|:---|:---|
| 1 | `User` | `Users` | `Users` | `UserConfiguration` | ✅ 7 điểm truy vấn | ✅ |
| 2 | `RefreshToken` | `RefreshTokens` | `RefreshTokens` | `RefreshTokenConfiguration` | ❌ (chỉ có trong navigation) | ⚠️ schema có, không dùng |
| 3 | `Role` | `Roles` | `Roles` | `RoleConfiguration` | ✅ 3 điểm | ✅ |
| 4 | `UserRole` | `UserRoles` | `UserRoles` | `UserRoleConfiguration` | ✅ 6 điểm | ✅ |
| 5 | `Permission` | `Permissions` | `Permissions` | `PermissionConfiguration` | ❌ | ⚠️ schema có, không dùng |
| 6 | `RolePermission` | `RolePermissions` | `RolePermissions` | `RolePermissionConfiguration` | ❌ (chỉ có trong navigation) | ⚠️ schema có, không dùng |
| 7 | `SellerProfile` | `SellerProfiles` | `SellerProfiles` | `SellerProfileConfiguration` | ❌ | ⚠️ schema có, không dùng |
| 8 | `ReputationScore` | `ReputationScores` | `ReputationScores` | `ReputationScoreConfiguration` | ✅ 1 điểm (`AuthService.cs:121`) | ✅ |
| 9 | `ReputationLog` | `ReputationLogs` | `ReputationLogs` | `ReputationLogConfiguration` | ❌ | ⚠️ schema có, không dùng |
| 10 | `UserReview` | `UserReviews` | `UserReviews` | `UserReviewConfiguration` | ❌ | ⚠️ schema có, không dùng |
| 11 | `UserReport` | `UserReports` | `UserReports` | `UserReportConfiguration` | ❌ | ⚠️ schema có, không dùng |
| 12 | `UserPenalty` | `UserPenalties` | `UserPenalties` | `UserPenaltyConfiguration` | ❌ | ⚠️ schema có, không dùng |
| 13 | `SystemConfig` | `SystemConfigs` | `SystemConfigs` | `SystemConfigConfiguration` | ❌ (chỉ seed) | ⚠️ schema có, không dùng |
| 14 | `AuditLog` | `AuditLogs` | `AuditLogs` | `AuditLogConfiguration` | ❌ | ⚠️ schema có, không dùng |
Ngoài 14 bảng trên, migration còn tạo **3 bảng hạ tầng của MassTransit**: `InboxState`, `OutboxMessage`, `OutboxState` (`AppDbContext.cs:36-38`).
**Quy ước chung của mọi entity (đúng 14/14):**
- Kế thừa `BaseEntity<TKey>` hoặc `AuditableEntity<TKey>` (`User.Domain/Base/BaseEntity.cs`), gồm: `Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `DeletedAt`.
- **Khóa chính kiểu `Guid`** — không entity nào dùng `int identity`.
- Id do code gán `Guid.NewGuid()` khi tạo mới (vd. `AuthService.cs:91,112,121`), **không** dùng `Guid.CreateVersion7()` và **không** có giá trị mặc định ở DB.
- Enum lưu dạng **chuỗi** qua `HasConversion<string>()` (vd. `UserConfiguration.cs:19-20`).
---
1. **`User` (Định danh người dùng toàn sàn)**:
   - `Id`: `Guid` (Primary Key).
   - `Email`: `string` (Unique Index, Max 255 chars) — `UserConfiguration.cs:15-16`.
   - `PasswordHash`: `string` (Max 500 chars; BCrypt — `BCrypt.Net-Next 4.0.3`).
   - `FirstName`, `LastName`: `string` (Max 150 chars mỗi trường).
   - `PhoneNumber`: `string?` (**Max 50** chars — đặc tả cũ ghi 20 là sai).
   - `IdentifyNumber`: `string?` (Max 50 chars).
   - `AvatarUrl`: `string?` (Max 500 chars).
   - `Gender`: `Gender` (`Unspecified = 0`, `Male = 1`, `Female = 2`, `Other = 3`), lưu dạng chuỗi.
   - `DateOfBirth`: `DateTimeOffset?`.
   - `Address`: `string?` (Max 1000 chars).
   - `Status`: `UserStatus` (`PendingVerification = 0`, `Active = 1`, `Banned = 2`, `Deleted = 3`); entity mặc định `Active`, nhưng `RegisterAsync` **luôn gán** `PendingVerification` (`AuthService.cs:99`).
   - `EmailVerifiedAt`: `DateTimeOffset?`.
   - `BannedAt`: `DateTimeOffset?`, `BannedBy`: `Guid?`, `BanReason`: `string?` (Max 1000).
   - Navigation: `BannedByUser`, `RefreshTokens`, `UserRoles`, `SellerProfile`, `ReputationScore`, `ReputationLogs`, `ReviewsGiven`, `ReviewsReceived`, `ReportsSubmitted`, `ReportsReceived`, `ReportsResolved`, `PenaltiesReceived`, `PenaltiesIssued` (13 navigation — trùng khớp đặc tả).
   - Xóa mềm: `HasQueryFilter(x => !x.IsDeleted)` — `UserConfiguration.cs:79`.
2. **`RefreshToken` (Quản lý phiên đa thiết bị)** — ⚠️ schema có, code chưa dùng:
   - `Id`: `Guid`.
   - `UserId`: `Guid` (FK $\rightarrow$ `User.Id`, cascade delete).
   - `TokenHash`: `string` (Unique Index, Max 500 chars).
   - `DeviceInfo`: `string?` (Max 255), `IpAddress`: `string?` (Max 100).
   - `IsRevoked`: `bool` (`HasDefaultValue(false)`); có property `[NotMapped] IsDisabled` alias hai chiều của `IsRevoked` (`User.Domain/Entities/RefreshToken.cs:15-20`).
   - `ExpiresAt`: `DateTimeOffset` (required), `RevokedAt`: `DateTimeOffset?`.
   - Navigation: `User`.
   - ⚠️ **Chưa có code nào tạo/đọc/thu hồi refresh token** — đây là bảng chờ cho tính năng JWT chưa triển khai.
3. **`Role` (Vai trò cốt lõi & mở rộng)**:
   - `Id`: **`Guid`** (đặc tả cũ ghi `int Identity` là **sai** — `Role : AuditableEntity<Guid>`).
   - `RoleCode`: `RoleType` (`Buyer = 0`, `Seller = 1`, `SupportStaff = 2`, `Admin = 3`), unique theo `dbml`, lưu dạng chuỗi.
   - `RoleName`: `string` (Max 150 chars).
   - `Description`: `string?` (Max 500 chars).
   - `IsActive`: `bool` (không có default ở config).
   - `IsSystemRole`: `bool` (`HasDefaultValue(false)`).
   - Navigation: `UserRoles`, `RolePermissions`.
   - Seed 4 role cố định: `Buyer` = `11111111-1111-1111-1111-111111111111`, `Seller` = `22222222-...`, `SupportStaff` = `33333333-...`, `Admin` = `44444444-...` (`UserSeedData.cs`).
4. **`UserRole` (Quan hệ người dùng - vai trò N-N)**:
   - `Id`: `Guid`.
   - `UserId`: `Guid` (FK $\rightarrow$ `User.Id`).
   - `RoleId`: **`Guid`** (FK $\rightarrow$ `Role.Id` — đặc tả cũ ghi `int` là **sai**).
   - `AssignedAt`: `DateTimeOffset`, `AssignedBy`: `Guid?`.
   - Unique Index: `(UserId, RoleId)` — `UserRoleConfiguration.cs`.
   - Navigation: `User`, `Role`.
5. **`Permission` (Danh mục đặc quyền hệ thống)** — ⚠️ schema có, code chưa dùng:
   - `Id`: **`Guid`** (đặc tả cũ ghi `int` là **sai**).
   - `PermissionCode`: `string` (Max 100, required — ⚠️ **không** có `HasIndex().IsUnique()`, xem `PermissionConfiguration.cs:12`), `PermissionName`: `string` (Max 255), `Category`: `string` (Max 100), `Description`: `string?` (Max 500).
   - Không có `HasQueryFilter` (xem §2).
   - Navigation: `RolePermissions`.
6. **`RolePermission` (Ma trận phân quyền Role-Permission)** — ⚠️ schema có, code chưa dùng:
   - `Id`: `Guid`.
   - `RoleId`: **`Guid`**, `PermissionId`: **`Guid`** (đặc tả cũ ghi `int` là **sai**).
   - `AssignedAt`: `DateTimeOffset`, `AssignedBy`: `Guid?` (theo entity; configuration không cấu hình thêm).
   - Unique Index: `(RoleId, PermissionId)`.
   - Navigation: `Role`, `Permission`.
7. **`SellerProfile` (Hồ sơ gian hàng người bán)** — ⚠️ schema có, code chưa dùng:
   - `Id`: `Guid`.
   - `UserId`: `Guid` (Unique FK $\rightarrow$ `User.Id`, quan hệ 1-1, cascade delete).
   - `ShopName`: `string` (Unique, Max 255).
   - `AvatarUrl`: `string?` (Max 500), `BusinessEmail`: `string?` (Max 255).
   - `IdCardFrontUrl`, `IdCardBackUrl`, `BusinessLicenseUrl`: `string?` (Max 500).
   - `PickupAddress`: `string` (Max 500), `ProvinceId`: `int`, `DistrictId`: `int`, `WardCode`: `string` (Max 100), `Hotline`: `string` (Max 50).
   - `Status`: `SellerProfileStatus` (`Pending = 0`, `Approved = 1`, `Rejected = 2`), mặc định `Pending`.
   - `RejectionReason`: `string?` (Max 1000).
   - `ReviewedBy`: `Guid?` (FK $\rightarrow$ `User.Id`, `SetNull`), `ReviewedAt`: `DateTimeOffset?`.
   - Navigation: `User`, `ReviewedByUser`.
8. **`ReputationScore` (Điểm uy tín tổng hợp)** — ✅ có dùng (tạo mới khi đăng ký):
   - `Id`: `Guid`.
   - `UserId`: `Guid` (Unique FK $\rightarrow$ `User.Id`, 1-1).
   - `CurrentScore`: `int` (mặc định **50** — `ReputationScore.cs:9`).
   - `TrustLevel`: `TrustLevel` (`Untrusted = 0`, `Probation = 1`, `Standard = 2`, `Good = 3`, `Excellent = 4`), mặc định `Standard`.
   - `TotalPositiveReviews`, `TotalNegativeReviews`, `PenaltyCount`: `int`.
   - Navigation: `User`.
9. **`ReputationLog` (Nhật ký biến động điểm số)** — ⚠️ schema có, code chưa dùng:
   - `Id`: **`Guid`** (đặc tả cũ ghi `int Identity` là **sai**).
   - `UserId`: `Guid` (FK $\rightarrow$ `User.Id`, cascade).
   - `ChangeAmount`: `int`.
   - `Reason`: `ReputationChangeReason` (`PositiveReview = 0`, `NegativeReview = 1`, `OrderCompleted = 2`, `AuctionPaymentTimeout = 3`, `DisputeCompensation = 4`, `ManualAdjustment = 5`).
   - `ReferenceType`: `ReferenceType?` (`Order = 0`, `Auction = 1`, `Dispute = 2`, `System = 3` — **có 4 giá trị**, đặc tả cũ chỉ ghi 2), `ReferenceId`: `string?` (Max 100).
   - `CreatedBy`: `Guid?` (FK $\rightarrow$ `User.Id`, `SetNull`).
   - Navigation: `User`, `CreatedByUser`.
10. **`UserReview` (Đánh giá giao dịch 2 chiều)** — ⚠️ schema có, code chưa dùng:
    - `Id`: `Guid`.
    - `ReviewerId`, `RevieweeId`: `Guid` (FK $\rightarrow$ `User.Id`, **`Restrict`** cả hai để tránh cascade cycle).
    - `ReviewerRole`: `ReviewerRole` (`Buyer = 0`, `Seller = 1`).
    - `ReferenceType`: `ReferenceType`, `ReferenceId`: `string` (Max 100).
    - `Rating`: `int` — ⚠️ **không có CHECK constraint 1..5** trong configuration (đặc tả cũ nói có).
    - `Comment`: `string?` (Max 2000).
    - `MediaUrls`: `List<string>` → `text[]`.
    - `IsDisputed`, `IsDisabled`: `bool` — **thực thể có thật cả hai** (`User.Domain/Entities/UserReview.cs`); ⚠️ không code nào đọc/ghi hai cờ này.
    - Navigation: `Reviewer`, `Reviewee`.
11. **`UserReport` (Tố cáo người dùng vi phạm)** — ⚠️ schema có, code chưa dùng:
    - `Id`: `Guid`.
    - `ReporterId`: `Guid`, `ReportedUserId`: `Guid` (FK $\rightarrow$ `User.Id`, `Restrict`).
    - `ViolationType`: `ReportViolationType` (`Fraud = 0`, `Harassment = 1`, `Counterfeit = 2`, `Spam = 3`, `Other = 4`).
    - `Description`: `string` (required, không giới hạn độ dài ở config).
    - `EvidenceUrls`: `List<string>` → `text[]`.
    - `Status`: `ReportStatus` (`Pending = 0`, `UnderReview = 1`, `Resolved = 2`, `Rejected = 3`).
    - `ResolvedBy`: `Guid?` (`SetNull`), `ResolutionNote`: `string?` (Max 2000).
    - Navigation: `Reporter`, `ReportedUser`, `ResolvedByUser`.
12. **`UserPenalty` (Biên bản xử phạt & trừ điểm)** — ⚠️ schema có, code chưa dùng:
    - `Id`: `Guid`.
    - `UserId`: `Guid` (FK $\rightarrow$ `User.Id`, cascade).
    - `ViolationType`: `PenaltyViolationType` (`PaymentTimeout = 0`, `AuctionNonPayment = 1`, `CancellationWithoutReason = 2`, `Fraud = 3`, `Other = 4`).
    - `Reason`: `string` (Max 500).
    - `PenaltyApplied`: `PenaltyType` (`Warning = 0`, `DeductPoints = 1`, `SuspendBidding3Days = 2`, `BanAccount = 3`, `Other = 4`).
    - `PointsDeducted`: `int`.
    - `Status`: `PenaltyStatus` (`Active = 0`, `Expired = 1`, `Revoked = 2`), mặc định `Active`.
    - `IssuedAt`: `DateTimeOffset` (mặc định `UtcNow`), `ExpiresAt`: `DateTimeOffset?`.
    - `CreatedBy`: `Guid?` (`SetNull`).
    - Navigation: `User`, `CreatedByUser`.
13. **`SystemConfig` (Cấu hình tham số động)** — ⚠️ schema có, code chưa dùng (chỉ seed):
    - `Id`: `Guid`.
    - `ConfigKey`: `string` (Unique Index, Max 100).
    - `ConfigValue`: `string` (Max 1000).
    - `DataType`: `ConfigDataType` (`String = 0`, `Int = 1`, `Decimal = 2`, `Boolean = 3`, `Json = 4`).
    - `Description`: `string?` (Max 500).
    - `UpdatedBy`: `Guid?`.
    - Seed 2 bản ghi: `DEFAULT_REPUTATION_SCORE = 50`, `AUTO_APPROVE_BUYER = true`.
14. **`AuditLog` (Nhật ký kiểm toán thao tác)** — ⚠️ schema có, code **chưa ghi** lần nào:
    - `Id`: `Guid` — ⚠️ **không phải UUID v7** (đặc tả cũ ghi "Version 7" là sai); entity dùng `Guid.NewGuid()` (`AuditLog.cs:7`).
    - `ServiceName`: `string` (Max 100).
    - `TraceId`: `string?` (Max 100) — ⚠️ **không chỗ nào gán giá trị** trong code hiện tại.
    - `UserId`, `UserName`: **`string?`** (Max 100 / 255) — đặc tả cũ ghi `string` là sai.
    - `Action`: `AuditAction` (`UserUpdate = 0`, `UserBan = 1`, `UserUnban = 2`, `SellerApprove = 3`, `SellerReject = 4`, `RoleAssign = 5`, ...).
    - `EntityName`, `EntityId`: `string` (Max 150, required).
    - `OldValues`, `NewValues`, `ChangedColumns`: `jsonb`.
    - `IpAddress` (Max 50), `UserAgent` (Max 500).
    - `CreatedAt`: `DateTimeOffset`, `IsSynced`: `bool`.
    - ⚠️ **Không có `HasQueryFilter`** dù `AuditLog` không kế thừa `ISoftDelete` trực tiếp — xem §2.
    - ⚠️ Không có interceptor/`SaveChanges` override nào ghi audit log ⇒ bảng luôn rỗng.
---
## 2. QUY CHUẨN EF CORE & PERSISTENCE
- Provider: `Npgsql.EntityFrameworkCore.PostgreSQL` **8.0.10** (`User.Infrastructure/User.Infrastructure.csproj`).
- Database: **`usermanagement_db`** — ⚠️ đặc tả cũ ghi `user_db` là **sai**. Nguồn: `User.Api/appsettings.json` (`ConnectionStrings:DefaultConnection`), `docker-compose.yml` (`POSTGRES_DB: usermanagement_db`), `User.Infrastructure/DependencyInjection.cs:19-24` (fallback hardcode cùng tên).
- Tên bảng: PascalCase mặc định theo tên Entity — ✅ đúng, **không** có lời gọi `builder.ToTable(...)` nào trong 14 configuration (grep = 0).
- Đăng ký DI: `AddDbContext<AppDbContext>` + `AddScoped<IUserDbContext>(sp => sp.GetRequiredService<AppDbContext>())` (`DependencyInjection.cs:19-24`).
- Enum mapping: `HasConversion<string>()` ✅ (áp dụng cho `User.Status`, `User.Gender`, `Role.RoleCode`, `SellerProfile.Status`, `UserReview.ReviewerRole`/`ReferenceType`, `UserReport.ViolationType`/`Status`, `UserPenalty.ViolationType`/`PenaltyApplied`/`Status`, `ReputationLog.Reason`/`ReferenceType`, `ReputationScore.TrustLevel`, `SystemConfig.DataType`, `AuditLog.Action`).
- Mảng URL: `List<string>` cấu hình `.HasColumnType("text[]")` ✅ (`UserReview.MediaUrls`, `UserReport.EvidenceUrls`).
- Khóa ngoại và chống cascade cycle ✅:
  - `Restrict`: `UserReview.ReviewerId`/`RevieweeId`, `UserReport.ReporterId`/`ReportedUserId`.
  - `SetNull`: `User.BannedBy`, `SellerProfile.ReviewedBy`, `ReputationLog.CreatedBy`, `UserReport.ResolvedBy`, `UserPenalty.CreatedBy`.
  - `Cascade`: `RefreshToken.UserId`, `SellerProfile.UserId`, `ReputationLog.UserId`, `UserPenalty.UserId`.
- Bộ lọc xóa mềm: ⚠️ **chỉ 8/14** entity có `HasQueryFilter(x => !x.IsDeleted)`: `User`, `RefreshToken`, `Role`, `SellerProfile`, `ReputationLog`, `UserReview`, `UserReport`, `UserPenalty`. **Thiếu 6**: `UserRole`, `Permission`, `RolePermission`, `ReputationScore`, `SystemConfig`, `AuditLog` ⇒ bản ghi đã xóa mềm của 6 bảng này **vẫn hiện** trong truy vấn.
---
## 3. MIGRATION & KHỞI TẠO SCHEMA
| Hạng mục | Hiện trạng | Bằng chứng |
|:---|:---|:---|
| Số migration | **1** (gộp toàn bộ lịch sử; 3 migration cũ `InitialUserSchema`, `SeedDefaultRoles`, `AddMassTransitOutboxInboxTables` đã bị xóa) | `User.Infrastructure/Migrations/` chỉ có `20261024103058_InitialUserSchema.cs` + `.Designer.cs` + `AppDbContextModelSnapshot.cs` |
| Số bảng tạo ra | **17** = 14 nghiệp vụ + `InboxState` + `OutboxMessage` + `OutboxState` | `20261024103058_InitialUserSchema.cs` |
| Seed trong migration | 4 role, 4 user (`admin@nexus.com`, `staff@nexus.com`, `seller@nexus.com`, `buyer@nexus.com` — mật khẩu hash của `Password123!`), 4 user-role, 1 seller profile (`Nexus Official Store`), 4 reputation score, 2 system config | `User.Infrastructure/Persistence/Seeds/UserSeedData.cs`, gọi từ `AppDbContext.cs:33` |
| Tự động migrate lúc khởi động | ✅ có, nhưng **nuốt lỗi** (chỉ `LogWarning`) | `User.Api/Program.cs:49-62` |
| Công cụ migrate | ❌ **không** có `./.tools/dotnet-ef`, **không** có `.config/dotnet-tools.json` | `ls .tools` → không tồn tại. Dùng `dotnet ef` (global tool) |
| Design-time factory | ❌ không có `IDesignTimeDbContextFactory` → mọi lệnh `dotnet ef` phải truyền `--startup-project User.Api` để lấy connection string | grep = 0 |