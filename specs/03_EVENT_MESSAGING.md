# 03 — SỰ KIỆN BẤT ĐỒNG BỘ QUA RABBITMQ (USER SERVICE EVENT MESSAGING)
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — **1/1 event publish** ✅ (`SendOtpEmailIntegrationEvent`, 2 điểm publish), **0 consumer** ❌. **3/4 event** trong đặc tả cũ (`UserRegisteredEvent`, `SellerApprovedEvent`, `UserRolesChangedEvent`) ❌ **không tồn tại trong code**.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
UserService sử dụng thư viện **MassTransit 8.3.0** kết nối với RabbitMQ để phát hành **duy nhất một** sự kiện: yêu cầu gửi email OTP.
> ⚠️ Bản đặc tả cũ ghi "MassTransit 8.4.1" — **sai**. Cả 4 package đều pin `8.3.0`: `MassTransit`, `MassTransit.RabbitMQ`, `MassTransit.EntityFrameworkCore` (`User.Infrastructure/User.Infrastructure.csproj`), `MassTransit.Abstractions` (`User.Application/User.Application.csproj`).
---
## 1. SỰ KIỆN PHÁT HÀNH (EVENTS PUBLISHED)
### 1.1 Sự kiện **có thật** trong code
| Tên Sự Kiện | Thời Điểm Kích Hoạt | Payload Dữ Liệu (đúng theo record) | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|:---|
| `SendOtpEmailIntegrationEvent` | Ngay sau khi lưu `User` + OTP vào DB/Redis khi đăng ký, và mỗi lần gửi lại OTP | `Email: string`, `FullName: string`, `Otp: string`, `ExpiresInMinutes: int = 5` | ✅ | Định nghĩa: `User.Application/Contracts/SendOtpEmailIntegrationEvent.cs:3-7` (namespace `Nexus.Contracts.Events`). Publish: `AuthService.cs:147` (đăng ký), `AuthService.cs:253` (gửi lại) |
Đặc điểm quan trọng của code hiện tại:
- `IPublishEndpoint` được inject **optional** (`IPublishEndpoint? publishEndpoint = null`, `AuthService.cs:21,30`) → khi bus offline, publish bị bọc `try/catch` và chỉ `LogWarning`, **không** làm fail đăng ký (`AuthService.cs:144-155`).
- Khi `IPublishEndpoint` là `null`, service log `[MESSAGE BUS OFFLINE] Simulated sending OTP email ...` kèm **mã OTP thật** ra log (`AuthService.cs:157`) — đây là rò rỉ OTP vào log, cần xử lý trước khi lên production.
- Tên endpoint dùng `SetKebabCaseEndpointNameFormatter()` (`DependencyInjection.cs:39`).
### 1.2 Sự kiện trong **đặc tả cũ** — ❌ KHÔNG tồn tại trong code
| Tên Sự Kiện (đặc tả) | Trạng thái | Ghi chú |
|:---|:---|:---|
| `UserRegisteredEvent` | ❌ | Đặc tả mục tiêu, chưa triển khai. Hiện tương đương nghiệp vụ là `SendOtpEmailIntegrationEvent` (chỉ gửi OTP, không mang `UserId`/`DisplayName`/`CreatedAt`) |
| `SellerApprovedEvent` | ❌ | Đặc tả mục tiêu — thậm chí `ApproveSeller` rpc cũng chưa implement (xem `02_API_GRPC_CONTRACTS.md` §1) |
| `UserRolesChangedEvent` | ❌ | Đặc tả mục tiêu — `AssignRoles` rpc chưa implement |
Kiểm chứng: grep `IConsumer|AddConsumer|ConfigureConsumer` trên toàn bộ `.cs` = **0**; grep `.Publish(` chỉ trả **2** vị trí, cùng một loại event (nêu trên).
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
- ⚠️ **Rủi ro thứ tự gọi (cần kiểm chứng runtime)**: trong `AuthService`, `Publish` được gọi **sau** `SaveChangesAsync` cuối cùng (`AuthService.cs:136` lưu Redis → `:139` `_dbContext.SaveChangesAsync` → `:147` publish) và **không có `SaveChangesAsync` nào sau đó**. Với bus outbox, message được thêm vào change tracker dưới dạng `OutboxMessage` và chỉ persisted khi `SaveChanges` chạy; nếu đúng như vậy thì message **không bao giờ được ghi vào bảng outbox** và sẽ mất khi scope dispose → email OTP không được gửi (chỉ còn log fallback). Đây là giả thuyết từ đọc code, **chưa** kiểm chứng bằng runtime; hướng sửa an toàn là publish **trước** `SaveChangesAsync` để message nằm trong cùng transaction.
- ⚠️ Không có `IConsumer` nào ⇒ `InboxState` hiện **không được dùng** (chỉ là bảng rỗng).
### 2.2 Những gì **không** có trong repo
- ❌ Không có `IRequestClient`/`Send<>` (không gọi đồng bộ service khác qua bus).
- ❌ Không có Saga/StateMachine.
- ❌ Không có retry policy, circuit breaker, hay DLQ configuration tường minh (dùng mặc định của MassTransit).