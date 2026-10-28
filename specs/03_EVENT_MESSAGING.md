# 03 — SỰ KIỆN BẤT ĐỒNG BỘ QUA RABBITMQ (USER SERVICE EVENT MESSAGING)
> **Trạng thái triển khai (2026-10-28):** ⚠️ Một phần — **2/2 event publish** ✅ (`SendOtpEmailIntegrationEvent` 2 điểm publish, `SellerApprovedIntegrationEvent` 1 điểm publish tại `SellerService.cs:372`), **0 consumer** ❌. **2 event** trong đặc tả cũ (`UserRegisteredEvent`, `UserRolesChangedEvent`) ❌ **chưa triển khai**.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-28.
UserService sử dụng thư viện **MassTransit 8.3.0** kết nối với RabbitMQ để phát hành **hai** sự kiện: yêu cầu gửi email OTP và thông báo duyệt tài khoản Seller kèm thông tin kho khởi tạo (Single Warehouse).
> ⚠️ Bản đặc tả cũ ghi "MassTransit 8.4.1" — **sai**. Cả 4 package đều pin `8.3.0`: `MassTransit`, `MassTransit.RabbitMQ`, `MassTransit.EntityFrameworkCore` (`User.Infrastructure/User.Infrastructure.csproj`), `MassTransit.Abstractions` (`User.Application/User.Application.csproj`).
---
## 1. SỰ KIỆN PHÁT HÀNH (EVENTS PUBLISHED)
### 1.1 Sự kiện **có thật** trong code
| Tên Sự Kiện | Thời Điểm Kích Hoạt | Payload Dữ Liệu (đúng theo record) | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|:---|
| `SendOtpEmailIntegrationEvent` | Ngay sau khi lưu `User` + OTP vào DB/Redis khi đăng ký, và mỗi lần gửi lại OTP | `Email: string`, `FullName: string`, `Otp: string`, `ExpiresInMinutes: int = 5` | ✅ | Định nghĩa: `User.Application/Contracts/SendOtpEmailIntegrationEvent.cs:3-7` (namespace `Nexus.Contracts.Events`). Publish: `AuthService.cs:147` (đăng ký), `AuthService.cs:253` (gửi lại) |
| `SellerApprovedIntegrationEvent` | Khi Staff duyệt hồ sơ đăng ký của Seller (`Status = Approved`) | `SellerId: Guid`, `UserId: Guid`, `ShopName: string`, `BusinessEmail: string?`, `Hotline: string`, `PickupAddress: string`, `ProvinceId: int`, `DistrictId: int`, `WardCode: string`, `ApprovedAt: DateTimeOffset` | ✅ | Định nghĩa: `User.Application/Contracts/SellerApprovedIntegrationEvent.cs:5-16` (namespace `Nexus.Contracts.Events`). Publish: `SellerService.cs:372` (Staff/Admin duyệt hồ sơ) |
Đặc điểm quan trọng của code hiện tại:
- `IPublishEndpoint` được inject **optional** (`IPublishEndpoint? publishEndpoint = null`, `AuthService.cs:21,30`, `SellerService.cs:35,46`) → khi bus offline, publish bị bọc `try/catch` và chỉ `LogWarning`, **không** làm fail luồng chính.
- Khi `IPublishEndpoint` là `null`, service log `[MESSAGE BUS OFFLINE] Simulated sending OTP email ...` kèm **mã OTP thật** ra log (`AuthService.cs:157`) — đây là rò rỉ OTP vào log, cần xử lý trước khi lên production.
- Tên endpoint dùng `SetKebabCaseEndpointNameFormatter()` (`DependencyInjection.cs:39`).
### 1.2 Sự kiện trong **đặc tả cũ** — Đối soát hiện trạng
| Tên Sự Kiện (đặc tả) | Trạng thái | Ghi chú |
|:---|:---|:---|
| `UserRegisteredEvent` | ❌ | Đặc tả mục tiêu, chưa triển khai. Hiện tương đương nghiệp vụ là `SendOtpEmailIntegrationEvent` (chỉ gửi OTP, không mang `UserId`/`DisplayName`/`CreatedAt`) |
| `SellerApprovedEvent` | ✅ | Đã chuẩn hóa và triển khai thành `SellerApprovedIntegrationEvent` (mang đầy đủ thông tin kho cho Inventory Service) tại `SellerService.cs:372` |
| `UserRolesChangedEvent` | ❌ | Đặc tả mục tiêu — `AssignRoles` hiện ghi nhận trực tiếp vào bảng `UserRoles` + `AuditLog`, chưa publish event qua bus |
Kiểm chứng: grep `IConsumer|AddConsumer|ConfigureConsumer` trên toàn bộ `.cs` = **0**; grep `.Publish(` trả về **3** vị trí (`AuthService.cs:147`, `AuthService.cs:253`, `SellerService.cs:372`).
---
## 2. QUY TẮC ĐẢM BẢO TIN NHẬN (OUTBOX PATTERN)
### 2.1 Cấu hình **thật** trong repo
`User.Infrastructure/DependencyInjection.cs:37-63`:
```csharp
services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddEntityFrameworkOutbox<AppDbContext>(o =>   // :41 — KHÔNG phải UserDbContext
    {
        o.UsePostgres();
        o.UseBusOutbox();
    });
    x.UsingRabbitMq((context, cfg) =>
    {
        var host = configuration["RabbitMq:Host"] ?? "localhost";
        var port = ushort.TryParse(configuration["RabbitMq:Port"] ?? "5672", out var p) ? p : (ushort)5672;
        // Username/Password đọc từ RabbitMq:User / RabbitMq:Password (mặc định "guest")
        cfg.Host(host, port, "/", h => { h.Username(user); h.Password(pass); });
        cfg.ConfigureEndpoints(context);
    });
});
```
- ✅ `AddEntityFrameworkOutbox<AppDbContext>` với `UsePostgres()` + `UseBusOutbox()` — đúng pattern.
- ✅ 3 entity outbox/inbox được map vào model: `AppDbContext.cs:36-38` (`AddInboxStateEntity`, `AddOutboxMessageEntity`, `AddOutboxStateEntity`).
- ✅ 3 bảng tương ứng tồn tại trong migration: `InboxState`, `OutboxMessage`, `OutboxState` (`User.Infrastructure/Migrations/20261024103058_InitialUserSchema.cs`).
- ✅ **Thứ tự gọi Outbox (Đã chuẩn hóa)**: Trong `AuthService`, `_publishEndpoint.Publish(...)` được gọi **trước** `_dbContext.SaveChangesAsync()` trong cả `RegisterAsync` và `ResendOtpAsync`. Nhờ đó entity `OutboxMessage` nằm trọn vẹn trong Change Tracker và được commit cùng transaction với dữ liệu người dùng xuống PostgreSQL, đảm bảo MassTransit Outbox Delivery Service quét và gửi tin cậy sang RabbitMQ.
- ⚠️ Không có `IConsumer` nào ⇒ `InboxState` hiện **không được dùng** (chỉ là bảng rỗng).
### 2.2 Những gì **không** có trong repo
- ❌ Không có `IRequestClient`/`Send<>` (không gọi đồng bộ service khác qua bus).
- ❌ Không có Saga/StateMachine.
- ❌ Không có retry policy, circuit breaker, hay DLQ configuration tường minh (dùng mặc định của MassTransit).