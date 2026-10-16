# 01 — PHÂN TẦNG CLEAN ARCHITECTURE & QUY TẮC PHỤ THUỘC (USER SERVICE)

Solution `User.sln` bao gồm 4 project tương ứng với 4 tầng kiến trúc sạch (Clean Architecture):

```
User.sln
├── User.Domain/           # Lõi nghiệp vụ độc lập 100% (Entities, Value Objects, Domain Exceptions)
├── User.Application/      # Các ca sử dụng nghiệp vụ (Commands, Queries, DTOs, Validators)
├── User.Infrastructure/   # Triển khai kỹ thuật (EF Core DbContext, Migrations, PasswordHasher, TokenService)
└── User.Api/              # Cổng giao tiếp gRPC Server, Program.cs, nạp DI, cấu hình Middleware
```

---

## 1. NGUYÊN TẮC LUỒNG PHỤ THUỘC (DEPENDENCY RULE)

```
[ User.Api ] ────────────┐
                         │
                         ▼
               [ User.Application ] ──▶ [ User.Domain ]
                         ▲
                         │
[ User.Infrastructure ] ─┘
```
- **`User.Domain`**: Không phụ thuộc vào bất kỳ project nào. Không import EF Core hay Web APIs.
- **`User.Application`**: Chỉ phụ thuộc vào `User.Domain`. Định nghĩa interface `IUserDbContext`, `IPasswordHasher`, `IJwtTokenGenerator`.
- **`User.Infrastructure`**: Phụ thuộc vào `User.Application` và `User.Domain`. Triển khai cụ thể các interface trên bằng EF Core 8 (Npgsql) và JWT library.
- **`User.Api`**: Điểm khởi động ứng dụng, nạp DI cho toàn bộ hệ thống và đăng ký `app.MapGrpcService<UsersGrpcService>()`.

---

## 2. QUY CHUẨN CODE TỪNG TẦNG

1. **`User.Domain`**:
   - Entities: `User`, `Role`, `UserRole`, `SellerRegistration`, `Permission`.
   - Sử dụng encapsulation (private setter), hàm khởi tạo hợp lệ và phương thức nghiệp vụ (ví dụ: `user.ApproveSeller()`, `user.ChangePassword(...)`).

2. **`User.Application`**:
   - Sử dụng mô hình **CQRS** kết hợp thư viện `MediatR` (hoặc tương đương).
   - Phân tách: `Commands/` (thay đổi trạng thái) và `Queries/` (chỉ đọc dữ liệu).
   - Truy vấn đọc (Queries) sử dụng `.AsNoTracking()` của EF Core để đạt hiệu năng tối đa.

3. **`User.Infrastructure`**:
   - `UserDbContext`: Kế thừa `DbContext`, triển khai `IUserDbContext`.
   - Cấu hình Fluent API trong `Configurations/` (`UserConfiguration.cs`, v.v.), không dùng Data Annotations bừa bãi trong Domain Entity.

4. **`User.Api`**:
   - `Services/UsersGrpcService.cs`: Kế thừa `UsersApi.UsersApiBase` do gRPC Protobuf sinh ra.
   - Nhận gRPC request $\rightarrow$ Gửi Command/Query qua MediatR $\rightarrow$ Map kết quả sang Protobuf Response.
