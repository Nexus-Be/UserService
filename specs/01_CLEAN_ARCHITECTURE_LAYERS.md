# 01 — PHÂN TẦNG CLEAN ARCHITECTURE & QUY TẮC PHỤ THUỘC (USER SERVICE)
> **Trạng thái triển khai (2026-10-29):** ✅ ĐÃ CHUẨN HÓA — Cấu trúc 4 tầng Clean Architecture chuẩn hóa: Feature Co-location tại `User.Application/Services/{Feature}/` (Auth, Profile, Seller, Admin), Consumer DI extension tại `User.Application/Consumers/ConsumerExtensions.cs`, và 1-to-1 Controllers tại `User.Api/Controllers/`. 100% RESTful HTTP/JSON (CẤM gRPC).
> **Nguồn sự thật:** code trong repo này (`User.sln`, `User.Application/Services/`, `User.Application/Consumers/`, `User.Api/Controllers/`).
> **Cập nhật lần cuối:** 2026-10-29.
Solution `User.sln` bao gồm 4 project tương ứng với 4 tầng kiến trúc sạch (Clean Architecture):
```
User.sln
├── User.Domain/           # Lõi nghiệp vụ độc lập 100% (14 Entities, 16 Enums, Domain Exceptions)
├── User.Application/      # Các ca sử dụng nghiệp vụ (AuthService + IUserDbContext/IRedisOtpService, DTOs)
├── User.Infrastructure/   # Triển khai kỹ thuật (EF Core 8 Npgsql DbContext, 14 Configuration, 1 Migration, Redis OTP, MassTransit)
└── User.Api/              # Cổng giao tiếp gRPC Server, Program.cs, nạp DI, cấu hình Middleware
```
Thực tế `User.sln` có **5 project** (thêm `User.Tests/` — 1 file, 9 `[Fact]`, 266 LOC).
---
## 1. NGUYÊN TẮC LUỒNG PHỤ THUỘC (DEPENDENCY RULE)
Sơ đồ **lý thuyết** (đặc tả):
```
[ User.Api ] ────────────┐
                         │
                         ▼
               [ User.Application ] ──▶ [ User.Domain ]
                         ▲
                         │
[ User.Infrastructure ] ─┘
```
Sơ đồ **thật** (đọc từ các `*.csproj`):
```
[ User.Api ] ──▶ [ User.Application ] ──▶ [ User.Domain ]
      │                   │
      └──▶ [ User.Infrastructure ] ──▶ [ User.Application ], [ User.Domain ]
                                     └──▶ (Domain: 0 reference, 0 package ✅)
[ User.Tests ] ──▶ [ User.Application ], [ User.Infrastructure ], [ User.Domain ]
```
| Quy tắc | Đặc tả | Hiện trạng | Bằng chứng |
|:---|:---|:---|:---|
| Domain không phụ thuộc project nào | ✅ | ✅ đúng | `User.Domain/User.Domain.csproj` — **0** `ProjectReference`, **0** `PackageReference` |
| Application chỉ phụ thuộc Domain | ✅ | ⚠️ **project ref** đúng (chỉ Domain) nhưng **package** lại có `Microsoft.EntityFrameworkCore 8.0.10`, `BCrypt.Net-Next 4.0.3`, `MassTransit.Abstractions 8.3.0` | `User.Application/User.Application.csproj` |
| Infrastructure phụ thuộc Application + Domain | ✅ | ✅ đúng | `User.Infrastructure/User.Infrastructure.csproj` |
| Api **không** được biết Infrastructure | (ngầm định) | ❌ **vi phạm**: `User.Api` reference `User.Infrastructure`, và `UsersGrpcService` inject thẳng `AppDbContext` | `User.Api/User.Api.csproj`; `User.Api/Services/UsersGrpcService.cs:19` |
| Tầng ngoài implement interface của tầng trong | ✅ | ⚠️ đúng về hình thức (`AppDbContext : IUserDbContext`) nhưng **băm mật khẩu nằm ở Application** (gọi trực tiếp `BCrypt.Net.BCrypt.HashPassword`) | `User.Infrastructure/Persistence/AppDbContext.cs:8`; `AuthService.cs:75,95` |
**Kết luận:** các interface `IPasswordHasher`, `IJwtTokenGenerator`, `ITokenService`/`TokenService` được đặc tả cũ nhắc tới **❌ KHÔNG tồn tại trong code** (grep = 0). Hiện chỉ có **2** interface hạ tầng: `IUserDbContext`, `IRedisOtpService`.
---
## 2. QUY CHUẨN CODE TỪNG TẦNG
1. **`User.Domain`** ✅:
   - Entities thật (14): `User`, `RefreshToken`, `Role`, `UserRole`, `Permission`, `RolePermission`, `SellerProfile`, `ReputationScore`, `ReputationLog`, `UserReview`, `UserReport`, `UserPenalty`, `SystemConfig`, `AuditLog`. **Không có** entity `SellerRegistration` (tên đặc tả cũ là sai → thật là `SellerProfile`).
   - Entity kế thừa `BaseEntity<TKey>` / `AuditableEntity<TKey>` (`User.Domain/Base/BaseEntity.cs`), **mọi khóa chính là `Guid`**, property `Id` để `public TKey Id { get; set; } = default!` → **không** dùng `int identity`.
   - ⚠️ Không dùng encapsulation thật: entity dùng `public required string Email { get; set; }` (setter công khai), và **không có** phương thức nghiệp vụ kiểu `user.ApproveSeller()`/`user.ChangePassword(...)` — nghiệp vụ nằm trong `AuthService`.
2. **`User.Application`** ⚠️:
   - ❌ **Không có CQRS/MediatR**: grep `MediatR` trên toàn repo (`.cs` + `.csproj`) = **0**. Không có thư mục `Commands/`, `Queries/`, `Validators/`.
   - Thực tế: mỗi use case là 1 cặp `I{Feature}Service`/`{Feature}Service` + `{Feature}Dtos`, đăng ký ở `User.Application/DependencyInjection.cs:10` (`AddScoped<IAuthService, AuthService>`).
   - ❌ `.AsNoTracking()` **không được dùng ở đâu** trong repo (chỉ các truy vấn có `.Include(...)` cho `RegisterAsync`, `VerifyOtpAsync`, `GetProfile`).
   - `IUserDbContext` nằm trong `User.Application/Interfaces/` và expose **cả 14 DbSet** — chấp nhận theo luật "no repository", nhưng là coupling trực tiếp vào EF Core (kéo theo package EF Core ở tầng Application).
3. **`User.Infrastructure`** ✅:
   - `AppDbContext`: `public class AppDbContext : DbContext, IUserDbContext` (`User.Infrastructure/Persistence/AppDbContext.cs:8`), `OnModelCreating` gọi `ApplyConfigurationsFromAssembly` (`:32`), seed (`:33`), và 3 entity MassTransit Outbox/Inbox (`:36-38`).
   - ⚠️ Có thêm `public class UserDbContext : AppDbContext` (`AppDbContext.cs:42`) nhưng **không được đăng ký DI** — DI chỉ đăng ký `AppDbContext` (`User.Infrastructure/DependencyInjection.cs:19-24`). Tài liệu cũ nói "UserDbContext triển khai IUserDbContext" là **gây nhầm lẫn**: lớp triển khai thật là `AppDbContext`.
   - ✅ Cấu hình Fluent API trong `Persistence/Configurations/` — **14 file** `*Configuration.cs`, không có `builder.ToTable(...)` (tên bảng = tên DbSet, PascalCase), enum lưu trực tiếp dạng số nguyên `integer` (int) trong DB.
4. **`User.Api`** ✅:
   - Điểm khởi chạy **REST API** — `builder.Services.AddControllers()` (`Program.cs:18`), `app.MapControllers()` (`Program.cs:60`), `app.MapGet("/health", ...)` (`Program.cs:61`).
   - Controllers: `AuthController.cs`, `UsersController.cs`, `AdminUsersController.cs`, `SellerController.cs`.
   - **Quy tắc Thin Controller**: Controller chỉ làm nhiệm vụ tiếp nhận HTTP request, gọi Service Interface trong Application, và trả về `IActionResult` với mã HTTP chuẩn. Cấm logic nghiệp vụ, cấm map anonymous object, cấm khai báo class DTO trong file controller.
---
## 3. CÁC VI PHẠM PHÂN TẦNG & TÌNH TRẠNG GIẢI QUYẾT
| # | Hạng mục | Hiện trạng | Giải pháp chuẩn hóa |
|--:|:---|:---|:---|
| 1 | `User.Api` phụ thuộc DTO nội bộ Controller | ✅ Đã dọn dẹp | Toàn bộ DTOs chuyển về `User.Application/Services/{Feature}/`, Controller dùng chung. |
| 2 | `User.Api` chứa logic chuyển đổi và gán role | ✅ Đã dọn dẹp | Chuyển toàn bộ use case logic về `AuthService` và `ProfileService`. |
| 3 | Domain Entity leak ra ngoài Controller | ✅ Đã dọn dẹp | `IAuthService` trả về `AuthUserResponseDto` thay vì entity `User`. |
| 4 | Cấu trúc DI từng tầng | ✅ Đạt chuẩn | `User.Application/DependencyInjection.cs` và `User.Infrastructure/DependencyInjection.cs` tự quản lý IoC. |
---
## 4. QUY CHUẨN CẤU TRÚC CHUẨN HÓA CHO USER SERVICE
1. **Giao tiếp đồng bộ (RESTful)**:
   - `User.Api/Controllers/`: Thin Dispatcher Controller, không logic, không inline DTO.
   - `User.Application/Services/{Feature}/`: Mỗi feature (`Auth`, `Profile`, `Admin`, `Seller`) chứa `I{Feature}Service.cs`, `{Feature}Service.cs`, và Request/Response DTOs dùng chung.
2. **Bãi bỏ hoàn toàn gRPC & Message Broker**:
   - Không sử dụng RabbitMQ, MassTransit hay Outbox/Inbox tables.
   - Gửi yêu cầu email OTP trực tiếp qua REST Typed Client (`IEmailNotificationClient`).
3. **Quản lý Dependency Injection**:
   - `User.Application/DependencyInjection.cs`: Đăng ký toàn bộ Application Services.
   - `User.Infrastructure/DependencyInjection.cs`: Đăng ký `AppDbContext` (hiện thực `IUserDbContext`), `RedisOtpService`, `JwtTokenGenerator`, và Typed REST Client `IEmailNotificationClient`.
4. **Validation**:
   - Input format validation đặt tại Request DTOs qua Data Annotations (`[Required]`, `[EmailAddress]`...).
   - Business validation đặt tại Application Services hoặc Domain Exceptions.
