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
   - Khi người dùng đăng ký, yêu cầu gửi lại OTP, hoặc quên mật khẩu/đăng ký seller, `AuthService` và `SellerService` gọi trực tiếp `IEmailNotificationClient.SendOtpEmailAsync` (`POST api/v1/notifications/email/otp`).
   - Khi xét duyệt hồ sơ người bán, `SellerService` gọi `IEmailNotificationClient.SendSellerDecisionEmailAsync` (`POST api/v1/notifications/seller-decisions`) sau khi commit DB.
   - Khi quản trị viên cấm hoặc mở cấm tài khoản, `AdminUserService` gọi `IEmailNotificationClient.SendAccountStatusChangedEmailAsync` (`POST api/v1/notifications/account-status-changes`) sau khi commit DB.
   - Khi người dùng đổi mật khẩu hoặc đặt lại mật khẩu thành công, `AuthService` gọi `IEmailNotificationClient.SendPasswordChangedEmailAsync` (`POST api/v1/notifications/password-changes`) sau khi commit DB.
   - Lớp triển khai `EmailNotificationClient` gửi HTTP POST sang `NotificationService` với `sourceEventId`, `traceId`, timeout 10s, bọc trong try-catch non-blocking. Nếu các sự kiện nghiệp vụ (không phải OTP) gửi thất bại, sự kiện được ghi vào bảng `NotificationDispatchLogs` để `NotificationRetryWorker` tự động thử lại giữ nguyên `sourceEventId` và payload.
