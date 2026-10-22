# 04 — MÔ HÌNH THỰC THỂ & CƠ SỞ DỮ LIỆU (USER SERVICE DATA PERSISTENCE - V2)

---

## 1. MÔ HÌNH THỰC THỂ CHÍNH (DOMAIN ENTITIES)

1. **`User` (Định danh người dùng toàn sàn)**:
   - `Id`: `Guid` (Primary Key).
   - `Email`: `string` (Unique, Max 255 chars).
   - `PasswordHash`: `string` (Hash bảo mật Argon2/BCrypt).
   - `FirstName`, `LastName`: `string` (Tách biệt họ và tên đệm / tên chính).
   - `PhoneNumber`: `string?` (Max 20 chars).
   - `IdentifyNumber`: `string?` (Số CCCD / Định danh cá nhân).
   - `AvatarUrl`: `string?` (Link ảnh đại diện cá nhân người dùng, Max 500 chars).
   - `Gender`: `Gender` (`Unspecified = 0`, `Male = 1`, `Female = 2`, `Other = 3`).
   - `DateOfBirth`: `DateTimeOffset?`.
   - `Address`: `string?`.
   - `Status`: `UserStatus` (`PendingVerification = 0`, `Active = 1`, `Banned = 2`, `Deleted = 3`).
   - `EmailVerifiedAt`: `DateTimeOffset?`.
   - `BannedAt`: `DateTimeOffset?`, `BannedBy`: `Guid?`, `BanReason`: `string?`.
   - Navigation: `BannedByUser`, `RefreshTokens`, `UserRoles`, `SellerProfile`, `ReputationScore`, `ReputationLogs`, `ReviewsGiven`, `ReviewsReceived`, `ReportsSubmitted`, `ReportsReceived`, `ReportsResolved`, `PenaltiesReceived`, `PenaltiesIssued`.

2. **`RefreshToken` (Quản lý phiên đa thiết bị)**:
   - `Id`: `Guid`.
   - `UserId`: `Guid` (FK $\rightarrow$ `User.Id`).
   - `TokenHash`: `string` (Unique Index, Max 500 chars).
   - `DeviceInfo`: `string?`, `IpAddress`: `string?`.
   - `IsRevoked`: `bool` (Đã thu hồi / vô hiệu hóa phiên; tương thích `IsDisabled`).
   - `ExpiresAt`: `DateTimeOffset`, `RevokedAt`: `DateTimeOffset?`.
   - Navigation: `User`.

3. **`Role` (Vai trò cốt lõi & mở rộng)**:
   - `Id`: `int` (Identity Primary Key).
   - `RoleCode`: `RoleType` (`Buyer = 0`, `Seller = 1`, `SupportStaff = 2`, `Admin = 3`).
   - `RoleName`: `string` (Max 150 chars).
   - `Description`: `string?`.
   - `IsActive`: `bool` (Chỉ 1 vai trò mặc định kích hoạt cho tài khoản mới).
   - `IsSystemRole`: `bool` (Bảo vệ vai trò hệ thống không cho phép xóa).
   - Navigation: `UserRoles`, `RolePermissions`.

4. **`UserRole` (Quan hệ người dùng - vai trò N-N)**:
   - `Id`: `Guid`.
   - `UserId`: `Guid` (FK $\rightarrow$ `User.Id`).
   - `RoleId`: `int` (FK $\rightarrow$ `Role.Id`).
   - `AssignedAt`: `DateTimeOffset`, `AssignedBy`: `Guid?`.
   - Unique Index: `(UserId, RoleId)`.
   - Navigation: `User`, `Role`.

5. **`Permission` (Danh mục đặc quyền hệ thống)**:
   - `Id`: `int` (Identity Primary Key).
   - `PermissionCode`: `string` (Unique, Max 100 chars).
   - `PermissionName`: `string`, `Category`: `string`, `Description`: `string?`.
   - Navigation: `RolePermissions`.

6. **`RolePermission` (Ma trận phân quyền Role-Permission)**:
   - `Id`: `Guid`.
   - `RoleId`: `int` (FK $\rightarrow$ `Role.Id`).
   - `PermissionId`: `int` (FK $\rightarrow$ `Permission.Id`).
   - `AssignedAt`: `DateTimeOffset`, `AssignedBy`: `Guid?`.
   - Unique Index: `(RoleId, PermissionId)`.
   - Navigation: `Role`, `Permission`.

7. **`SellerProfile` (Hồ sơ gian hàng người bán)**:
   - `Id`: `Guid`.
   - `UserId`: `Guid` (Unique FK $\rightarrow$ `User.Id`, quan hệ 1-1).
   - `ShopName`: `string` (Unique, Max 255 chars).
   - `AvatarUrl`: `string?` (Logo / Avatar riêng của Shop, Max 500 chars).
   - `BusinessEmail`: `string?` (Email liên hệ công việc riêng của gian hàng, Max 255 chars).
   - `IdCardFrontUrl`: `string?` (Ảnh mặt trước CCCD duyệt mở Shop).
   - `IdCardBackUrl`: `string?` (Ảnh mặt sau CCCD).
   - `BusinessLicenseUrl`: `string?` (Ảnh Giấy phép kinh doanh nếu có).
   - `PickupAddress`: `string`, `ProvinceId`: `int`, `DistrictId`: `int`, `WardCode`: `string`, `Hotline`: `string`.
   - `Status`: `SellerProfileStatus` (`Pending = 0`, `Approved = 1`, `Rejected = 2`).
   - `RejectionReason`: `string?`.
   - `ReviewedBy`: `Guid?` (FK $\rightarrow$ `User.Id`), `ReviewedAt`: `DateTimeOffset?`.
   - Navigation: `User`, `ReviewedByUser`.

8. **`ReputationScore` (Điểm uy tín tổng hợp)**:
   - `Id`: `Guid`.
   - `UserId`: `Guid` (Unique FK $\rightarrow$ `User.Id`, quan hệ 1-1).
   - `CurrentScore`: `int` (Mặc định 50 điểm).
   - `TrustLevel`: `TrustLevel` (`Untrusted = 0`, `Probation = 1`, `Standard = 2`, `Good = 3`, `Excellent = 4`).
   - `TotalPositiveReviews`, `TotalNegativeReviews`, `PenaltyCount`: `int`.
   - Navigation: `User`.

9. **`ReputationLog` (Nhật ký biến động điểm số)**:
   - `Id`: `int` (Identity Primary Key).
   - `UserId`: `Guid` (FK $\rightarrow$ `User.Id`).
   - `ChangeAmount`: `int` (+1, -1, -2, -10...).
   - `Reason`: `ReputationChangeReason`.
   - `ReferenceType`: `ReferenceType?`, `ReferenceId`: `string?`.
   - `CreatedBy`: `Guid?` (FK $\rightarrow$ `User.Id`, Admin/Support can thiệp chỉnh điểm).
   - Navigation: `User`, `CreatedByUser`.

10. **`UserReview` (Đánh giá giao dịch 2 chiều)**:
    - `Id`: `Guid`.
    - `ReviewerId`: `Guid` (FK $\rightarrow$ `User.Id`, người gửi đánh giá).
    - `RevieweeId`: `Guid` (FK $\rightarrow$ `User.Id`, người nhận đánh giá).
    - `ReviewerRole`: `ReviewerRole` (`Buyer = 0`, `Seller = 1`).
    - `ReferenceType`: `ReferenceType` (`Order = 0`, `Auction = 1`).
    - `ReferenceId`: `string`.
    - `Rating`: `int` (CHECK 1..5).
    - `Comment`: `string?`.
    - `MediaUrls`: `List<string>` (Mảng link ảnh/video mở hộp sản phẩm, kiểu `text[]`).
    - `IsDisputed`: `bool`, `IsDisabled`: `bool`.
    - Navigation: `Reviewer`, `Reviewee`.

11. **`UserReport` (Tố cáo người dùng vi phạm - MỚI)**:
    - `Id`: `Guid`.
    - `ReporterId`: `Guid` (FK $\rightarrow$ `User.Id`, người gửi tố cáo).
    - `ReportedUserId`: `Guid` (FK $\rightarrow$ `User.Id`, người bị tố cáo vi phạm).
    - `ViolationType`: `ReportViolationType` (`Fraud = 0`, `Harassment = 1`, `Counterfeit = 2`, `Spam = 3`, `Other = 4`).
    - `Description`: `string`.
    - `EvidenceUrls`: `List<string>` (Mảng link ảnh/video bằng chứng, kiểu `text[]`).
    - `Status`: `ReportStatus` (`Pending = 0`, `UnderReview = 1`, `Resolved = 2`, `Rejected = 3`).
    - `ResolvedBy`: `Guid?` (FK $\rightarrow$ `User.Id`, nhân viên xử lý).
    - `ResolutionNote`: `string?`.
    - Navigation: `Reporter`, `ReportedUser`, `ResolvedByUser`.

12. **`UserPenalty` (Biên bản xử phạt & trừ điểm - MỚI)**:
    - `Id`: `Guid`.
    - `UserId`: `Guid` (FK $\rightarrow$ `User.Id`, tài khoản bị xử phạt).
    - `ViolationType`: `PenaltyViolationType` (`PaymentTimeout = 0`, `AuctionNonPayment = 1`, `CancellationWithoutReason = 2`, `Fraud = 3`, `Other = 4`).
    - `Reason`: `string`.
    - `PenaltyApplied`: `PenaltyType` (`Warning = 0`, `DeductPoints = 1`, `SuspendBidding3Days = 2`, `BanAccount = 3`, `Other = 4`).
    - `PointsDeducted`: `int` (Số điểm uy tín bị trừ).
    - `Status`: `PenaltyStatus` (`Active = 0`, `Expired = 1`, `Revoked = 2`).
    - `IssuedAt`: `DateTimeOffset`.
    - `ExpiresAt`: `DateTimeOffset?`.
    - `CreatedBy`: `Guid?` (FK $\rightarrow$ `User.Id`, Admin/Support ban hành, NULL nếu cron tự động).
    - Navigation: `User`, `CreatedByUser`.

13. **`SystemConfig` (Cấu hình tham số động)**:
    - `Id`: `Guid`.
    - `ConfigKey`: `string` (Unique Index).
    - `ConfigValue`: `string`.
    - `DataType`: `ConfigDataType` (`String = 0`, `Int = 1`, `Decimal = 2`, `Boolean = 3`, `Json = 4`).
    - `Description`: `string?`.
    - `UpdatedBy`: `Guid?` (FK $\rightarrow$ `User.Id`).

14. **`AuditLog` (Nhật ký kiểm toán thao tác)**:
    - `Id`: `Guid` (Version 7).
    - `ServiceName`: `string`.
    - `TraceId`: `string?`.
    - `UserId`, `UserName`: `string`.
    - `Action`: `AuditAction`.
    - `EntityName`, `EntityId`: `string`.
    - `OldValues`, `NewValues`, `ChangedColumns`: `jsonb`.
    - `IpAddress`, `UserAgent`: `string`.
    - `CreatedAt`: `DateTimeOffset`.
    - `IsSynced`: `bool`.

---

## 2. QUY CHUẨN EF CORE & PERSISTENCE

- Provider: `Npgsql.EntityFrameworkCore.PostgreSQL`.
- Database: `user_db`.
- Tên bảng: PascalCase mặc định theo tên Entity C# (**Không dùng `builder.ToTable(...)`**).
- Enum mapping: Lưu trữ chuỗi string tường minh qua `HasConversion<string>()`.
- Mảng URL: Mảng chuỗi C# `List<string>` cấu hình `.HasColumnType("text[]")`.
- Khóa ngoại và ràng buộc vòng xóa (Cascade Delete):
  - Khóa ngoại tự trỏ hoặc nhiều khóa ngoại cùng trỏ về `User` được cấu hình an toàn bằng `DeleteBehavior.Restrict` hoặc `DeleteBehavior.SetNull` nhằm tránh xung đột cascade cycle.
- Bộ lọc xóa mềm: Kích hoạt `HasQueryFilter(x => !x.IsDeleted)` trên các thực thể kế thừa `ISoftDelete`.
