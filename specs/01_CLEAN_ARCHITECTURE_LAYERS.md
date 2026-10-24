# 01 — PHÂN TẦNG CLEAN ARCHITECTURE & QUY TẮC PHỤ THUỘC (USER SERVICE)
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — cấu trúc 4 tầng **có thật** (5 project: Domain 34 file/520 LOC, Application 8/374, Infrastructure 19/949, Api 3/324, Tests 1/266), nhưng **2 quy tắc phụ thuộc bị vi phạm** (Api → Infrastructure, Application → EF Core package) và **0/4 thành phần lý thuyết** (`MediatR`/CQRS, `IPasswordHasher`, `IJwtTokenGenerator`, `TokenService`) tồn tại trong code.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
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
   - ✅ Cấu hình Fluent API trong `Persistence/Configurations/` — **14 file** `*Configuration.cs`, không có `builder.ToTable(...)` (tên bảng = tên DbSet, PascalCase), enum map bằng `HasConversion<string>()`.
4. **`User.Api`** ⚠️:
   - `Services/UsersGrpcService.cs` — `public sealed class UsersGrpcService : UsersApi.UsersApiBase` (`:14`), override **4/13** rpc.
   - ❌ Không có MediatR: `UsersGrpcService` nhận **trực tiếp** `IAuthService` + `AppDbContext`, không qua Command/Query bus. `GetProfile` tự query DB (`:127-137`).
   - Đăng ký endpoint: `app.MapGrpcService<UsersGrpcService>()` (`Program.cs:64`), `app.MapGet("/health", ...)` (`:65`), Swagger + gRPC Reflection chỉ ở Development (`:41-45`).
---
## 3. CÁC VI PHẠM PHÂN TẦNG ĐANG TỒN TẠI (PHẢI BIẾT TRƯỚC KHI SỬA)
| # | Vi phạm | Hệ quả | Bằng chứng |
|--:|:---|:---|:---|
| 1 | `User.Api` → `User.Infrastructure` (project reference + inject `AppDbContext`) | Không thể thay Infra mà không sửa Api; test tầng Api phải kéo cả EF Core/Postgres | `User.Api/User.Api.csproj`; `User.Api/Services/UsersGrpcService.cs:19,127` |
| 2 | `User.Application` → package EF Core/BCrypt/MassTransit | "Application thuần nghiệp vụ" chỉ đúng ở mức project reference, không đúng ở mức package | `User.Application/User.Application.csproj` |
| 3 | `User.Tests` **không** reference `User.Api` | Không thể viết test cho 4 rpc đã implement; 0 test gRPC | `User.Tests/User.Tests.csproj` |
| 4 | `GlobalExceptionHandlerMiddleware` là middleware HTTP, **không** phải gRPC interceptor | Lỗi gRPC chỉ được map thủ công trong từng method của `UsersGrpcService` (`:111-117`) | `User.Api/Middleware/GlobalExceptionHandlerMiddleware.cs` |