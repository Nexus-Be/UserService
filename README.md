# USER SERVICE — ARCHITECTURE & FOLDER STRUCTURE GUIDE
## (CẨM NANG CẤU TRÚC THƯ MỤC & PHÁT TRIỂN DÀNH CHO DEVELOPER VÀ AI AGENT)
> **Trạng thái triển khai (2026-10-25):** ⚠️ Một phần — **5/13 rpc** của `UsersApi` đã implement (`Register`, `VerifyOtp`, `ResendOtp`, `Login`, `GetProfile`); **8/13 rpc** còn là đặc tả mục tiêu. `Login` ✅ **đã có code kèm phát hành JWT** (`JwtTokenGenerator`). Cột `Username` ✅ **đã có trong entity và CSDL Neon**. Messaging: **1 event publish** (`SendOtpEmailIntegrationEvent`), **0 consumer**.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-25.
> **Thông tin dịch vụ** (số liệu kiểm chứng trực tiếp):
> - **Service Port**: `8001` ✅ — khớp cả 3 nơi: `Dockerfile:3` (`EXPOSE 8001`), `Dockerfile:4` (`ENV ASPNETCORE_URLS=http://+:8001`), `User.Api/Properties/launchSettings.json` (`"applicationUrl": "http://localhost:8001"`). Kestrel chạy HTTP/2 h2c cho gRPC.
> - **Database**: PostgreSQL `usermanagement_db`, port host `5433` ✅ — `User.Api/appsettings.json` (`ConnectionStrings:DefaultConnection`) và `docker-compose.yml` (`POSTGRES_DB: usermanagement_db`, `${USER_DB_PORT:-5433}`). Cột `Username` có Filtered Unique Index `IX_Users_Username`.
> - **Nhiệm vụ ĐÃ CÓ trong code**: đăng ký tài khoản (kèm username) + gửi OTP email, xác thực OTP, gửi lại OTP, đăng nhập (`rpc Login` qua email/username + sinh JWT), đọc hồ sơ người dùng, gán role `Buyer` mặc định, seed 4 role + 4 user mẫu.
> - **Nhiệm vụ CHƯA CÓ trong code** (đặc tả mục tiêu): cập nhật hồ sơ, refresh token, nâng cấp/duyệt seller, quản trị user–role–permission. Xem chi tiết ở §6.
---
## 1. SƠ ĐỒ PHÂN TẦNG VÀ CẤU TRÚC THƯ MỤC CHUẨN
`UserService` được xây dựng theo chuẩn **Clean Architecture 4 tầng** khử ghép nối hoàn toàn:
```
src/Services/User/
│
├── User.sln                                            # Solution thật (KHÔNG phải "UserService.sln")
│
├── User.Domain/                                        # 1. TẦNG DOMAIN (POCO 100%, ZERO DEPENDENCY)
│   ├── Base/                                           # BaseEntity<TKey>, AuditableEntity<TKey>, IAuditable, ISoftDelete
│   ├── Entities/                                       # 14 entity: User (có Username), Role, UserRole, SellerProfile, RefreshToken...
│   ├── Enums/                                          # 16 enum: UserStatus, RoleType, TrustLevel...
│   └── Exceptions/                                     # DomainException (+ BusinessRuleValidationException định nghĩa cùng file)
│
├── User.Application/                                   # 2. TẦNG APPLICATION (USE CASES & BUSINESS LOGIC)
│   ├── Interfaces/                                     # Interface hạ tầng
│   │   ├── IUserDbContext.cs                           # 14 DbSet + SaveChangesAsync
│   │   ├── IRedisOtpService.cs                         # Lưu/đọc/thu hồi OTP trên Redis
│   │   └── IJwtTokenGenerator.cs                       # Hợp đồng sinh mã JWT Token
│   │
│   ├── Models/                                         # OtpEntry, OtpStatus (KHÔNG có thư mục Common/)
│   │
│   ├── Contracts/                                      # SendOtpEmailIntegrationEvent (namespace Nexus.Contracts.Events)
│   │
│   ├── Services/                                       # CÁC DỊCH VỤ XỬ LÝ USE CASE (THEO TÍNH NĂNG)
│   │   └── Auth/                                       # Module Xác thực & Đăng ký
│   │       ├── IAuthService.cs                         # 4 method: RegisterAsync, VerifyOtpAsync, ResendOtpAsync, LoginAsync
│   │       ├── AuthService.cs                          # Nghiệp vụ Register / VerifyOtp / ResendOtp / Login
│   │       └── AuthDtos.cs                             # RegisterDto (có Username), LoginDto, LoginResultDto
│   │
│   └── DependencyInjection.cs                          # AddApplicationServices() → AddScoped<IAuthService, AuthService>
│
├── User.Infrastructure/                                # 3. TẦNG INFRASTRUCTURE (CÔNG NGHỆ & HẠ TẦNG CỤ THỂ)
│   ├── Persistence/                                    # Quản lý CSDL PostgreSQL & EF Core 8
│   │   ├── AppDbContext.cs                             # AppDbContext : DbContext, IUserDbContext (+ lớp con UserDbContext KHÔNG được đăng ký DI)
│   │   ├── Configurations/                             # 14 Fluent API Configuration (*Configuration.cs)
│   │   └── Seeds/                                      # UserSeedData.cs — seed role/user/user-role/seller/reputation/system-config
│   │
│   ├── Migrations/                                     # Nằm ở gốc User.Infrastructure/
│   │   ├── 20261024103058_InitialUserSchema.cs         # Migration khởi tạo schema
│   │   └── 20261025003659_AddUsernameToUser.cs         # Migration thêm cột Username + Filtered Unique Index
│   │
│   ├── Services/                                       # CÁC DỊCH VỤ HẠ TẦNG KỸ THUẬT (MODULE HÓA)
│   │   ├── Redis/                                      # Module tích hợp Redis (RedisOtpService)
│   │   └── Jwt/                                        # Module phát hành JWT (JwtTokenGenerator)
│   │
│   └── DependencyInjection.cs                          # AddInfrastructureServices(configuration)
│
├── User.Api/                                           # 4. TẦNG API / HOST (ENTRY POINT & MIDDLEWARE)
│   ├── Middleware/                                     # MIDDLEWARE TOÀN CỤC CỦA SERVICE
│   │   └── GlobalExceptionHandlerMiddleware.cs         # Format lỗi ProblemDetails (RFC 9110), ghi log kèm traceId
│   ├── Services/                                       # gRPC Server Service
│   │   └── UsersGrpcService.cs                         # : UsersApi.UsersApiBase — 5/13 rpc override (Register, VerifyOtp, ResendOtp, Login, GetProfile)
│   ├── Contracts/                                      # shop.proto (hợp đồng gRPC, GrpcServices="Server")
│   ├── Properties/launchSettings.json                  # Port chạy local thật: 8001
│   ├── appsettings.json                                # ConnectionStrings + RabbitMq + Redis + Otel + Jwt (được đọc bởi JwtTokenGenerator)
│   └── Program.cs                                      # Kestrel HTTP/1+HTTP/2 (h2c), DI, gRPC, Swagger, /health, auto-migrate
│
└── User.Tests/                                         # 5. TẦNG KIỂM THỬ (UNIT TESTS)
    └── AuthServiceTests.cs                             # 9 [Fact] — chỉ phủ AuthService (Register/VerifyOtp/ResendOtp)
```
**Ghi chú cấu trúc so với bản cũ:** các mục sau trong tài liệu cũ là **sai thực tế** và đã được sửa ở trên:
- `User.Application/Common/Models/` ❌ không tồn tại → thật là `User.Application/Models/`.
- `User.Infrastructure/Persistence/Migrations/` ❌ → thật là `User.Infrastructure/Migrations/`.
- `UserService.sln` ❌ → thật là `User.sln`.
- `.tools/dotnet-ef` ❌ → không tồn tại trong repo (xem §5).
---
## 2. NGUYÊN TẮC PHÂN CHIA TRÁCH NHIỆM: PHẦN NÀO LÀM CÁI NÀO?
1. **`User.Domain`** ✅:
   - Chứa Entity nghiệp vụ thuần túy C#.
   - Không import EntityFrameworkCore — `User.Domain.csproj` chỉ có `ProjectReference` tới chính nó (0 PackageReference).
2. **`User.Application`** ✅:
   - Chứa ca sử dụng (Use Case). Mọi Use Case nằm trong `Services/{Feature}/`.
   - Mỗi feature có 1 thư mục riêng gồm: `I{Feature}Service.cs`, `{Feature}Service.cs`, và `{Feature}Dtos.cs`.
   - Các interface của hạ tầng (DbContext, OTP cache) được đặt trong thư mục `Interfaces/`.
   - Không phụ thuộc cứng vào công nghệ cụ thể (ví dụ: `IRedisOtpService` cho phép thay Redis bằng backend khác chỉ ở tầng Infra).
   - ❌ **Không có CQRS/MediatR**: 0 tham chiếu `MediatR` trong toàn repo; toàn bộ use case là service class gọi trực tiếp.
3. **`User.Infrastructure`** ✅:
   - Triển khai No-Repository Pattern: dùng trực tiếp `AppDbContext` thông qua interface `IUserDbContext` (`User.Infrastructure/DependencyInjection.cs:19-24` đăng ký `AddDbContext<AppDbContext>` rồi bind `IUserDbContext` vào chính instance đó).
   - Các dịch vụ hạ tầng được đóng gói theo thư mục riêng trong `Services/{Module}/` (ví dụ `Services/Redis/`).
4. **`User.Api`** ⚠️:
   - Tầng đón nhận request mỏng: 3 file `.cs` (324 LOC).
   - `GlobalExceptionHandlerMiddleware` bắt lỗi và chuẩn hóa JSON lỗi — nhưng đây là middleware **HTTP**, không phải gRPC interceptor: lỗi phát sinh trong gRPC được `UsersGrpcService` tự bọc `RpcException` tại từng method, không đi qua middleware này.
   - `UsersGrpcService` **không hoàn toàn mỏng**: ngoài việc gọi Application Service, nó inject thẳng `AppDbContext` và tự truy vấn DB cho `GetProfile` (`User.Api/Services/UsersGrpcService.cs:19`, `:127-137`) → tạo phụ thuộc `User.Api → User.Infrastructure` (vi phạm sơ đồ phân tầng lý thuyết, xem `specs/01_CLEAN_ARCHITECTURE_LAYERS.md` §3).
---
## 3. QUY TRÌNH 5 BƯỚC THÊM TÍNH NĂNG MỚI (DÀNH CHO DEV & AI AGENT)
Khi cần thêm tính năng mới (ví dụ: Quản lý Hồ sơ Người bán - `SellerProfile`):
1. **Bước 1: Domain**: Tạo Entity `SellerProfile.cs` trong `User.Domain/Entities/` và Enum liên quan.
2. **Bước 2: Infrastructure Persistence**: Tạo `SellerProfileConfiguration.cs` trong `User.Infrastructure/Persistence/Configurations/`, thêm `DbSet<SellerProfile>` vào `AppDbContext` **và** khai báo property tương ứng trong `IUserDbContext` (hai file phải luôn đồng bộ — hiện cả hai đều có đủ 14 DbSet), rồi tạo migration mới.
3. **Bước 3: Application Use Case**:
   - Tạo thư mục: `User.Application/Services/SellerProfile/`.
   - Tạo `SellerProfileDtos.cs`: Định nghĩa DTO request/response.
   - Tạo `ISellerProfileService.cs`: Định nghĩa ca sử dụng (ví dụ: `RegisterSellerAsync`, `ApproveSellerAsync`).
   - Tạo `SellerProfileService.cs`: Viết code nghiệp vụ thao tác với `IUserDbContext`.
4. **Bước 4: Dependency Injection**: Đăng ký `services.AddScoped<ISellerProfileService, SellerProfileService>();` trong `User.Application/DependencyInjection.cs`.
5. **Bước 5: API Presentation**: `override` method tương ứng trong `User.Api/Services/UsersGrpcService.cs` (rpc phải có sẵn trong `User.Api/Contracts/shop.proto`) và viết test trong `User.Tests/`.
   - ⚠️ Lưu ý: `User.Tests.csproj` **không** reference `User.Api`, nên muốn test tầng gRPC phải bổ sung reference (hiện không test được `UsersGrpcService`).
---
## 4. QUY CHUẨN THÔNG BÁO LỖI (MANDATORY ERROR RULES)
Mọi thông báo lỗi bắt buộc tuân theo 3 nguyên tắc:
1. **100% English**: Tuyệt đối không dùng tiếng Việt — ✅ hiện trạng đạt (`AuthService.cs`, `GlobalExceptionHandlerMiddleware.cs`).
2. **Sentence case**: Viết hoa duy nhất chữ cái đầu (`"Email is already registered in the system."`) — ✅ hiện trạng đạt.
3. **Error Code SCREAMING_SNAKE_CASE** — ✅ hiện trạng đạt. Danh sách mã đang tồn tại trong code:
| Mã lỗi | Nơi phát sinh | gRPC status map |
|:---|:---|:---|
| `VAL_INVALID_EMAIL` | `User.Application/Services/Auth/AuthService.cs:44,164,225` | `InvalidArgument` (mặc định) |
| `VAL_INVALID_PASSWORD` | `AuthService.cs:49` | `InvalidArgument` |
| `VAL_INVALID_FIRST_NAME` / `VAL_INVALID_LAST_NAME` | `AuthService.cs:54,59` | `InvalidArgument` |
| `VAL_INVALID_OTP` | `AuthService.cs:170` | `InvalidArgument` |
| `EMAIL_ALREADY_EXISTS` | `AuthService.cs:70` | `AlreadyExists` |
| `USER_NOT_FOUND` | `AuthService.cs:182,233` | `NotFound` |
| `OTP_EXPIRED` / `OTP_ALREADY_USED` / `OTP_INVALID` | `AuthService.cs:194,199,204,209` | `FailedPrecondition` / `FailedPrecondition` / `InvalidArgument` |
| `USER_ALREADY_VERIFIED` | `AuthService.cs:239` | `FailedPrecondition` |
| `VAL_INVALID_ARGUMENT` / `INTERNAL_SERVER_ERROR` | `User.Api/Middleware/GlobalExceptionHandlerMiddleware.cs:48,55` | (chỉ HTTP ProblemDetails) |
Bảng map tập trung tại `User.Api/Services/UsersGrpcService.cs:111-117`. **Không** có mã `UNAUTHORIZED`/`FORBIDDEN` nào được phát ra trong code hiện tại (vì chưa có login).
---
## 5. CÁCH CHẠY VÀ KIỂM THỬ ĐỘC LẬP SERVICE
```bash
# 1. Bật database của User Service:
cd src/Services/User
docker compose up -d user-db
# 2. Cập nhật migration CSDL:
#    ❌ "./.tools/dotnet-ef" KHÔNG tồn tại trong repo và repo không có tool manifest
#       (.config/dotnet-tools.json cũng không có). Dùng EF tool toàn cục:
dotnet tool install --global dotnet-ef      # nếu chưa cài
dotnet ef database update --project User.Infrastructure --startup-project User.Api
# 3. Chạy service:
dotnet run --project User.Api
#    → gRPC + HTTP lắng nghe tại http://localhost:8001
#    → Swagger UI: http://localhost:8001/swagger (chỉ ở môi trường Development)
#    → Health check: http://localhost:8001/health  → {"status":"Healthy","service":"User.Api"}
# 4. Chạy toàn bộ bài test:
dotnet test User.sln -c Release
```
**Lưu ý vận hành:**
- `Program.cs:49-62` tự động gọi `db.Database.Migrate()` lúc khởi động và **nuốt lỗi** (chỉ log warning) nếu không kết nối được DB → service vẫn lên nhưng mọi truy vấn DB sẽ fail. Log sẽ có dòng `Database migration deferred on startup: ...`.
- Ở chế độ Development, `AddGrpcReflection()` được bật (`Program.cs:25,44`) → có thể gọi thử bằng `grpcurl` mà không cần file proto.
- Redis là **tùy chọn về mặt kỹ thuật nhưng bắt buộc về mặt nghiệp vụ**: `RedisOtpService` chấp nhận `IConnectionMultiplexer?` null và khi Redis offline thì OTP **không được lưu ở đâu cả** (chỉ log warning, `RedisOtpService.cs:46-49`), nên `VerifyOtp` sẽ luôn trả `OTP_EXPIRED`. Muốn test luồng đăng ký phải có Redis sống.
---
## 6. TRẠNG THÁI TRIỂN KHAI THỰC TẾ (ĐỐI CHIẾU ĐẶC TẢ)
### 6.1 gRPC — `UsersApi` (13 rpc khai báo trong `User.Api/Contracts/shop.proto:47-64`)
| # | rpc | Trạng thái | Bằng chứng |
|--:|:---|:---|:---|
| 1 | `Register` | ✅ | `UsersGrpcService.cs:25` |
| 2 | `VerifyOtp` | ✅ | `UsersGrpcService.cs:72` |
| 3 | `ResendOtp` | ✅ | `UsersGrpcService.cs:89` |
| 4 | `GetProfile` | ✅ | `UsersGrpcService.cs:119` |
| 5 | `Login` | ❌ | Khai báo `shop.proto:52`; **không override** → gọi vào trả `UNIMPLEMENTED` |
| 6 | `UpdateProfile` | ❌ | `shop.proto:53` — không override |
| 7 | `ListUsers` | ❌ | `shop.proto:54` — không override |
| 8 | `AssignRoles` | ❌ | `shop.proto:55` — không override |
| 9 | `ListRoles` | ❌ | `shop.proto:56` — không override |
| 10 | `ListPermissions` | ❌ | `shop.proto:57` — không override |
| 11 | `RequestSellerUpgrade` | ❌ | `shop.proto:58` — không override |
| 12 | `ApproveSeller` | ❌ | `shop.proto:59` — không override |
| 13 | `CreateUser` | ❌ | `shop.proto:61` — không override |
**Đếm: 4/13 ✅ · 9/13 ❌.**
### 6.2 Xác thực & JWT
- ✅ `BCrypt.Net-Next 4.0.3` hash mật khẩu: `AuthService.cs:75,95`.
- ❌ **Không có code phát hành token**: grep `Jwt|JWT|GenerateToken|SecurityToken` trên toàn bộ `.cs` = **0 kết quả**; `IAuthService.cs:9-11` chỉ có 3 method; entity `RefreshToken` có trong DB nhưng **chưa từng được ghi**.
- ⚠️ `User.Api/appsettings.json` **có** section `Jwt` (`Issuer`, `Audience`, `Signature`, `ExpirationInMinutes: 60`) nhưng **không code nào đọc** section này. Đây là cấu hình chờ, chưa phải tính năng.
### 6.3 Messaging
- ✅ Publish `SendOtpEmailIntegrationEvent` (định nghĩa `User.Application/Contracts/SendOtpEmailIntegrationEvent.cs:3`, namespace `Nexus.Contracts.Events`) tại 2 chỗ: `AuthService.cs:147` và `AuthService.cs:253`.
- ✅ MassTransit **8.3.0** + EF Outbox `UsePostgres()`/`UseBusOutbox()`: `User.Infrastructure/DependencyInjection.cs:37-41`.
- ❌ **0 consumer** — grep `IConsumer|AddConsumer|ConfigureConsumer` = 0.
- ❌ `UserRegisteredEvent`, `SellerApprovedEvent`, `UserRolesChangedEvent` (đặc tả cũ) **không tồn tại trong code**.
### 6.4 Dữ liệu
- ✅ 14 DbSet ↔ 14 entity ↔ 14 bảng trong `dbml/service/user.dbml`; migration tạo **17 bảng** (14 nghiệp vụ + `InboxState`, `OutboxState`, `OutboxMessage`).
- ⚠️ Chỉ **4/14 DbSet** được code nghiệp vụ đọc/ghi: `Users`, `UserRoles`, `Roles`, `ReputationScores`. **10 DbSet còn lại** chỉ khai báo + cấu hình EF, chưa có truy vấn nào: `RefreshTokens`, `Permissions`, `RolePermissions`, `SellerProfiles`, `ReputationLogs`, `UserReviews`, `UserReports`, `UserPenalties`, `SystemConfigs`, `AuditLogs`.
### 6.5 Kiểm thử
- ⚠️ 1 file test / 9 `[Fact]`, **100% phủ `AuthService`**; 0 test cho gRPC, 0 test cho publish event, 0 test cho `RedisOtpService`.
- `User.Tests.csproj` reference `User.Application`, `User.Infrastructure`, `User.Domain` — **không** reference `User.Api`.
### 6.6 Khả năng quan sát (Observability)
- ❌ Không có OpenTelemetry: không có `AddOpenTelemetry` trong bất kỳ `.cs` nào (chỉ có section `Otel:Endpoint` trong appsettings, không ai đọc).
- ⚠️ `AuditLog.TraceId` (`User.Domain/Entities/AuditLog.cs:9`) tồn tại trong schema nhưng **không chỗ nào gán giá trị**; bảng `AuditLogs` cũng chưa từng được ghi.
- ⚠️ Trace id của lỗi lấy từ `Activity.Current?.Id ?? HttpContext.TraceIdentifier` (`GlobalExceptionHandlerMiddleware.cs:34`) → chỉ đúng khi có OTel; nếu không, là id cục bộ của Kestrel và không nối được với `x-trace-id` từ Gateway.