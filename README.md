# USER SERVICE — ARCHITECTURE & FOLDER STRUCTURE GUIDE
## (CẨM NANG CẤU TRÚC THƯ MỤC & PHÁT TRIỂN DÀNH CHO DEVELOPER VÀ AI AGENT)

> **Thông tin dịch vụ**:
> - **Service Port**: `8001` (HTTP/1.1 REST & HTTP/2 gRPC)
> - **Database**: PostgreSQL (`usermanagement_db`), Port máy host: `5433`
> - **Nhiệm vụ chính**: Đăng ký, đăng nhập, phát hành JWT, User Profile, RBAC 5 vai trò, Quản lý Seller Profile.

---

## 1. SƠ ĐỒ PHÂN TẦNG VÀ CẤU TRÚC THƯ MỤC CHUẨN

`UserService` được xây dựng theo chuẩn **Clean Architecture 4 tầng** khử ghép nối hoàn toàn:

```
src/Services/User/
│
├── User.Domain/                                       # 1. TẦNG DOMAIN (POCO 100%, ZERO DEPENDENCY)
│   ├── Base/                                          # BaseEntity, IAuditable, ISoftDelete
│   ├── Entities/                                      # User, Role, UserRole, SellerProfile, RefreshToken...
│   ├── Enums/                                         # UserStatus, RoleType, TrustLevel...
│   └── Exceptions/                                    # DomainException, BusinessRuleValidationException
│
├── User.Application/                                  # 2. TẦNG APPLICATION (USE CASES & BUSINESS LOGIC)
│   ├── Interfaces/                                    # Interfaces của CSDL và các dịch vụ hạ tầng ngoài
│   │   ├── IUserDbContext.cs                          # Abstraction của DbContext
│   │   └── IRedisOtpService.cs                        # Abstraction dịch vụ Redis OTP
│   │
│   ├── Common/                                        # Các tiện ích và model dùng chung trong Application
│   │   └── Models/                                    # OtpEntry, Result...
│   │
│   ├── Services/                                      # CÁC DỊCH VỤ XỬ LÝ USE CASE (THEO TÍNH NĂNG)
│   │   └── Auth/                                      # Module Xác thực & Đăng ký
│   │       ├── IAuthService.cs                        # Interface ca sử dụng Auth
│   │       ├── AuthService.cs                         # Class xử lý nghiệp vụ (Register, VerifyOtp, ResendOtp)
│   │       └── AuthDtos.cs                            # DTOs riêng của Auth (RegisterDto, VerifyOtpDto...)
│   │
│   └── DependencyInjection.cs                         # Đăng ký AddApplicationServices()
│
├── User.Infrastructure/                               # 3. TẦNG INFRASTRUCTURE (CÔNG NGHỆ & HẠ TẦNG CỤ THỂ)
│   ├── Persistence/                                   # Quản lý CSDL PostgreSQL & EF Core 8
│   │   ├── AppDbContext.cs                            # Triển khai IUserDbContext
│   │   ├── Configurations/                            # 14 Fluent API Configurations (*Configuration.cs)
│   │   ├── Migrations/                                # Code-first Database Migrations
│   │   └── Seeds/                                     # Dữ liệu mẫu vai trò (Roles, SystemConfigs)
│   │
│   ├── Services/                                      # CÁC DỊCH VỤ HẠ TẦNG KỸ THUẬT (MODULE HÓA)
│   │   └── Redis/                                     # Module tích hợp Redis
│   │       ├── RedisOptions.cs                        # Cấu hình Options đọc từ appsettings
│   │       └── RedisOtpService.cs                     # Triển khai IRedisOtpService
│   │
│   └── DependencyInjection.cs                         # Đăng ký AddInfrastructureServices(configuration)
│
├── User.Api/                                          # 4. TẦNG API / HOST (ENTRY POINT & MIDDLEWARE)
│   ├── Middleware/                                    # MIDDLEWARE TOÀN CỤC CỦA SERVICE
│   │   └── GlobalExceptionHandlerMiddleware.cs        # Bắt mọi lỗi ngoại lệ, format RFC 9110 ProblemDetails
│   ├── Services/                                      # gRPC Server Service
│   │   └── UsersGrpcService.cs                        # Triển khai UsersApi.UsersApiBase từ Protobuf
│   ├── Contracts/                                     # shop.proto (Hợp đồng gRPC)
│   └── Program.cs                                     # Khởi động Kestrel HTTP/1 & HTTP/2, gọi DI App & Infra
│
└── User.Tests/                                        # 5. TẦNG KIỂM THỬ (UNIT & INTEGRATION TESTS)
    └── AuthServiceTests.cs                            # Kiểm thử luồng đăng ký, mã hóa mật khẩu, OTP
```

---

## 2. NGUYÊN TẮC PHÂN CHIA TRÁCH NHIỆM: PHẦN NÀO LÀM CÁI NÀO?

1. **`User.Domain`**:
   - Chứa Entity nghiệp vụ thuần túy C#.
   - **Tuyệt đối cấm** import EntityFrameworkCore hay các thư viện hạ tầng vào đây.
2. **`User.Application`**:
   - Chứa ca sử dụng (Use Case). Mọi Use Case nằm trong `Services/{Feature}/`.
   - Mỗi feature có 1 thư mục riêng gồm: `I{Feature}Service.cs`, `{Feature}Service.cs`, và `{Feature}Dtos.cs`.
   - Các interface của hạ tầng (DbContext, Cache, Email) được đặt trong thư mục `Interfaces/`.
   - Không phụ thuộc cứng vào công nghệ cụ thể (sau này đổi từ Redis sang Memcached chỉ cần sửa tầng Infra).
3. **`User.Infrastructure`**:
   - Triển khai No-Repository Pattern (ADR 05): dùng trực tiếp `AppDbContext` thông qua interface `IUserDbContext`.
   - Các dịch vụ hạ tầng được đóng gói theo thư mục riêng trong `Services/{Module}/` (ví dụ `Services/Redis/`).
4. **`User.Api`**:
   - Tầng đón nhận request cực kỳ mỏng.
   - `GlobalExceptionHandlerMiddleware` là middleware toàn cục tự động bắt lỗi và chuẩn hóa JSON lỗi.
   - `UsersGrpcService` chỉ làm nhiệm vụ nhận Protobuf request $\rightarrow$ gọi Application Service $\rightarrow$ trả về Protobuf response.

---

## 3. QUY TRÌNH 5 BƯỚC THÊM TÍNH NĂNG MỚI (DÀNH CHO DEV & AI AGENT)

Khi cần thêm tính năng mới (ví dụ: Quản lý Hồ sơ Người bán - `SellerProfile`):

1. **Bước 1: Domain**: Tạo Entity `SellerProfile.cs` trong `User.Domain/Entities/` và Enum liên quan.
2. **Bước 2: Infrastructure Persistence**: Tạo `SellerProfileConfiguration.cs` trong `User.Infrastructure/Persistence/Configurations/` và thêm `DbSet<SellerProfile>` vào `IUserDbContext` & `AppDbContext`.
3. **Bước 3: Application Use Case**:
   - Tạo thư mục: `User.Application/Services/SellerProfile/`.
   - Tạo `SellerProfileDtos.cs`: Định nghĩa DTO request/response.
   - Tạo `ISellerProfileService.cs`: Định nghĩa ca sử dụng (ví dụ: `RegisterSellerAsync`, `ApproveSellerAsync`).
   - Tạo `SellerProfileService.cs`: Viết code nghiệp vụ thao tác với `IUserDbContext`.
4. **Bước 4: Dependency Injection**: Đăng ký `services.AddScoped<ISellerProfileService, SellerProfileService>();` trong `User.Application/DependencyInjection.cs`.
5. **Bước 5: API Presentation**: Gọi service trong `User.Api/Services/UsersGrpcService.cs` và viết test trong `User.Tests/`.

---

## 4. QUY CHUẨN THÔNG BÁO LỖI (MANDATORY ERROR RULES)

Mọi thông báo lỗi bắt buộc tuân theo 3 nguyên tắc:
1. **100% English**: Tuyệt đối không dùng tiếng Việt.
2. **Sentence case**: Viết hoa duy nhất chữ cái đầu (`"Email is already registered in the system."`).
3. **Error Code SCREAMING_SNAKE_CASE**: Ví dụ `EMAIL_ALREADY_EXISTS`, `USER_NOT_FOUND`, `VAL_INVALID_INPUT`.

---

## 5. CÁCH CHẠY VÀ KIỂM THỬ ĐỘC LẬP SERVICE

```bash
# 1. Bật database của User Service:
cd src/Services/User
docker compose up -d user-db

# 2. Cập nhật migration CSDL:
./.tools/dotnet-ef database update --project User.Infrastructure --startup-project User.Api

# 3. Chạy service:
dotnet run --project User.Api

# 4. Chạy toàn bộ bài test:
dotnet test User.Tests/User.Tests.csproj
```
