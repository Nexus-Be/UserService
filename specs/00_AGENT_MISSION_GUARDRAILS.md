# 00 — NHIỆM VỤ CỐT LÕI & RÀO CHẮN CHỐNG ẢO GIÁC (USER SERVICE)
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — **4/13 rpc** gRPC ✅ (`Register`, `VerifyOtp`, `ResendOtp`, `GetProfile`) / **9/13 rpc** ❌ còn là đặc tả; **chưa có code đăng nhập và chưa có code phát hành JWT** (grep `Jwt|GenerateToken|SecurityToken` trên toàn bộ `.cs` = 0). Messaging: **1 event publish**, **0 consumer**.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
---
## 1. NHIỆM VỤ & RANH GIỚI MIỀN (BOUNDED CONTEXT)
**UserService** chịu trách nhiệm độc quyền về việc quản lý định danh người dùng và kiểm soát truy cập (Identity & Access Management) cho toàn bộ hệ thống Nexus:
| # | Nhiệm vụ | Trạng thái | Bằng chứng (code thật) |
|--:|:---|:---|:---|
| 1 | Đăng ký người dùng (`Register`) | ✅ | `User.Api/Services/UsersGrpcService.cs:25` → `AuthService.RegisterAsync` (`User.Application/Services/Auth/AuthService.cs:38`) |
| 2 | Xác thực OTP email (`VerifyOtp`, `ResendOtp`) | ✅ | `UsersGrpcService.cs:72,89` |
| 3 | Đọc hồ sơ người dùng (`GetProfile`) | ✅ | `UsersGrpcService.cs:119` (đọc `x-user-id` header ở `:121`) |
| 4 | Xác thực đăng nhập (`Login`) | ❌ | `rpc Login` khai báo ở `User.Api/Contracts/shop.proto:52` nhưng **không override** → gọi vào trả `UNIMPLEMENTED`. Đặc tả mục tiêu, chưa triển khai |
| 5 | Phát hành JSON Web Token (JWT) | ❌ | `IAuthService.cs:9-11` chỉ có `RegisterAsync`/`VerifyOtpAsync`/`ResendOtpAsync`; **0 chỗ sinh token** trong repo. Section `Jwt` trong `appsettings.json` **không được code nào đọc**. Đặc tả mục tiêu, chưa triển khai |
| 6 | Cập nhật hồ sơ (`UpdateProfile`) | ❌ | `shop.proto:53` — không override. Đặc tả mục tiêu, chưa triển khai |
| 7 | Quản lý quy trình nâng cấp người bán (`SellerRegistration` / `SellerProfile`) | ⚠️ | Entity + bảng `SellerProfiles` + seed 1 seller **có** (`User.Domain/Entities/SellerProfile.cs`, `User.Infrastructure/Persistence/Seeds/UserSeedData.cs`), nhưng **0 rpc** `RequestSellerUpgrade`/`ApproveSeller` được implement và **0 dòng code nghiệp vụ** đọc/ghi `DbSet<SellerProfile>` |
| 8 | Quản trị phân quyền theo vai trò (RBAC) | ⚠️ | Có schema `Roles`/`UserRoles`/`Permissions`/`RolePermissions` + seed 4 role (`Buyer`, `Seller`, `SupportStaff`, `Admin`); code chỉ **gán role Buyer mặc định khi đăng ký** (`AuthService.cs:109-117`). `AssignRoles`/`ListRoles`/`ListPermissions` ❌ chưa implement |
| 9 | Cung cấp gRPC Server tại cổng `8001` | ✅ | `User.Api/Program.cs:24,64`; `Dockerfile:3-4`; `Properties/launchSettings.json` |
**Tên class/enum thật cần dùng khi viết tài liệu hoặc code:**
- Entity hồ sơ seller là **`SellerProfile`** (không phải `SellerRegistration`).
- DbContext triển khai `IUserDbContext` là **`AppDbContext`** (`User.Infrastructure/Persistence/AppDbContext.cs:8`). Lớp `UserDbContext` có tồn tại (`AppDbContext.cs:42`) nhưng **không được đăng ký trong DI** — DI chỉ đăng ký `AppDbContext` (`User.Infrastructure/DependencyInjection.cs:19`).
- Enum vai trò là **`RoleType`** với 4 giá trị `Buyer`, `Seller`, `SupportStaff`, `Admin` (`User.Domain/Enums/RoleType.cs`).
---
## 2. NHỮNG ĐIỀU CẤM KỴ ĐỐI VỚI AI AGENTS (ANTI-HALLUCINATION GUARDRAILS)
1. **TUYỆT ĐỐI KHÔNG DÙNG REPOSITORY PATTERN BỌC NGOÀI EF CORE** ✅ (hiện trạng tuân thủ):
   - Không sử dụng Repository Pattern bọc ngoài EF Core.
   - Sử dụng trực tiếp `AppDbContext`/`DbSet<T>` trong tầng Application thông qua interface `IUserDbContext` (`User.Application/Interfaces/IUserDbContext.cs:8-24`, 14 DbSet + `SaveChangesAsync`).
   - Tuyệt đối **KHÔNG** tạo `IUserRepository`, `GenericRepository`, `UnitOfWork`. Hiện repo có **0 file** khớp các tên này.
2. **TUYỆT ĐỐI KHÔNG LƯU MẬT KHẨU DẠNG PLAIN TEXT** ✅ (hiện trạng tuân thủ):
   - Mật khẩu được băm bằng **BCrypt** (`BCrypt.Net-Next 4.0.3`) với salt do thư viện sinh: `AuthService.cs:75`, `AuthService.cs:95`.
   - ⚠️ **Không có Argon2id** trong repo (không có package Argon2 nào) — đặc tả "Argon2id" là lựa chọn tương lai, hiện chỉ có BCrypt.
3. **TUYỆT ĐỐI KHÔNG THAY ĐỔI CỔNG 8001 VÀ DATABASE 5433** ✅ (hiện trạng khớp):
   - Service lắng nghe `http://localhost:8001` (host) / `http://+:8001` (container).
   - Chuỗi kết nối DB: `Host=localhost;Port=5433;Database=usermanagement_db;Username=postgres` (`User.Api/appsettings.json`). Trong Docker network, host đổi thành `user-db` và port `5432` (`docker-compose.yml`, biến `ConnectionStrings__DefaultConnection`) — **port 5433 chỉ là port publish ra máy host**, không phải port trong container.
   - Tên DB đúng là **`usermanagement_db`**; tên `user_db` xuất hiện trong một số tài liệu đặc tả cũ là **sai** so với code.
4. **TUYỆT ĐỐI KHÔNG TỰ Ý SỬA PROTOBUF MÀ KHÔNG ĐỒNG BỘ GATEWAY** ⚠️:
   - File hợp đồng trong repo này là `User.Api/Contracts/shop.proto` (được biên dịch với `GrpcServices="Server"`, xem `User.Api/User.Api.csproj`).
   - Việc "khớp 100% với ApiGateway" **không thể kiểm chứng từ repo này** (repo User là git submodule độc lập, không đọc được `shop.proto` của Gateway). Khi sửa proto, phải kiểm tra chéo thủ công ở cả hai repo.
   - Proto có ghi chú `SchemaVersion: v3 — mọi thay đổi từ đây phải là ADDITIVE` (`shop.proto:7`).
---
## 3. TÓM TẮT NHANH CHO AGENT MỚI (ĐỌC TRƯỚC KHI LÀM)
- Muốn đọc/ghi DB → inject `IUserDbContext`. Muốn gọi use case → inject `IAuthService` (hiện là use case **duy nhất**).
- Muốn thêm rpc → (1) thêm `override` trong `UsersGrpcService`, (2) proto **đã** có sẵn 13 rpc nên thường không cần sửa proto.
- Muốn thêm event → định nghĩa record trong `User.Application/Contracts/`, publish qua `IPublishEndpoint` (đang inject **optional** `IPublishEndpoint?` — nếu bus offline sẽ log warning và **không** ném lỗi).
- **Không** được hứa "login/JWT" trong tài liệu như thể đã có — xem bảng §1.