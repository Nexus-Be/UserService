# 03 — SỰ KIỆN BẤT ĐỒNG BỘ QUA RABBITMQ (USER SERVICE EVENT MESSAGING)

UserService sử dụng thư viện **MassTransit 8.4.1** kết nối với RabbitMQ để phát hành các sự kiện trạng thái thay đổi trong miền Identity.

---

## 1. SỰ KIỆN PHÁT HÀNH (EVENTS PUBLISHED)

| Tên Sự Kiện | Thời Điểm Kích Hoạt | Payload Dữ Liệu | Services Quan Tâm |
|:---|:---|:---|:---|
| `UserRegisteredEvent` | Người dùng hoàn tất đăng ký tài khoản mới thành công | `UserId`, `Username`, `Email`, `DisplayName`, `CreatedAt` | `NotificationService` (Gửi email chào mừng), `AuditService` (Ghi log). |
| `SellerApprovedEvent` | Nhân viên vận hành phê duyệt đơn đăng ký bán hàng | `UserId`, `ShopName`, `ApprovedByStaffId`, `ApprovedAt` | `CatalogService` (Khởi tạo kho ảo cho Seller), `NotificationService` (Thông báo duyệt thành công). |
| `UserRolesChangedEvent`| Quản trị viên thay đổi vai trò (roles) của một user | `UserId`, `OldRoles`, `NewRoles`, `ChangedByAdminId` | `AuditService` (Ghi nhận vết bảo mật). |

---

## 2. QUY TẮC ĐẢM BẢO TIN NHẬN (OUTBOX PATTERN)

- Cấu hình MassTransit Transactional Outbox trong `User.Infrastructure`:
```csharp
builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<UserDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
    });
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", 5672, "/", h => { ... });
        cfg.ConfigureEndpoints(context);
    });
});
```
- Khi lưu `User` vào DB, event được ghi vào bảng `OutboxMessage` cùng một transaction. Worker của MassTransit sẽ chịu trách nhiệm gửi tin nhắn lên RabbitMQ, đảm bảo không bao giờ thất lạc event.
