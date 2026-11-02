# 03 — QUY CHUẨN GIAO TIẾP VÀ BÃI BỎ MESSAGE BROKER (USER SERVICE)

> **Trạng thái triển khai (2026-10-30):** 🛑 ĐÃ BÃI BỎ HOÀN TOÀN RABBITMQ & MASSTRANSIT — Toàn bộ cơ chế Publisher, Outbox/Inbox tables đã được loại bỏ khỏi UserService. Việc gửi email xác thực OTP, duyệt/từ chối seller, ban/unban tài khoản được thực hiện trực tiếp qua REST API client đồng bộ (`IEmailNotificationClient`).
> **Nguồn sự thật:** code (`User.Infrastructure/Services/Notification/EmailNotificationClient.cs`).
> **Cập nhật lần cuối:** 2026-11-02.

---

## 1. NGUYÊN TẮC BÃI BỎ MESSAGE BROKER

1. **Gỡ bỏ hoàn toàn MassTransit**:
   - Toàn bộ các package `MassTransit`, `MassTransit.RabbitMQ`, `MassTransit.EntityFrameworkCore` đã được gỡ bỏ khỏi UserService.
   - Các bảng hạ tầng Outbox/Inbox (`OutboxMessage`, `OutboxState`, `InboxState`) không còn tham gia vào luồng nghiệp vụ.

2. **Chuyển sang Typed REST Client đồng bộ**:
   - Khi người dùng đăng ký hoặc yêu cầu gửi lại OTP, `AuthService` gọi trực tiếp `IEmailNotificationClient.SendOtpEmailAsync` (`POST api/v1/notifications/email/otp`).
   - Khi xét duyệt hồ sơ người bán, `SellerService` gọi `IEmailNotificationClient.SendSellerApprovedEmailAsync` (`POST api/v1/notifications/email/seller-approved`) hoặc `SendSellerRejectedEmailAsync` (`POST api/v1/notifications/email/seller-rejected`).
   - Khi quản trị viên cấm hoặc mở cấm tài khoản, `AdminUserService` gọi `IEmailNotificationClient.SendUserBannedEmailAsync` (`POST api/v1/notifications/email/user-banned`) hoặc `SendUserUnbannedEmailAsync` (`POST api/v1/notifications/email/user-unbanned`).
   - Lớp triển khai `EmailNotificationClient` gửi HTTP POST sang `NotificationService` với timeout và retry an toàn, bọc trong try-catch non-blocking để không làm gián đoạn transaction chính.
   - Tránh hoàn toàn việc rò rỉ OTP hay thông tin nhạy cảm vào log khi message broker offline.
