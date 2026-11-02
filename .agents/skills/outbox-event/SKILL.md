---
name: outbox-event
description: >-
  Inter-service communication and task processing guide for UserService in 100% Synchronous RESTful architecture: defines synchronous REST API integration patterns (HttpClientFactory with Polly), in-memory background task processing (Channels & BackgroundService), and legacy RabbitMQ/MassTransit Outbox audit references.
---

# Inter-Service Communication & Legacy Outbox Audit Skill

> ⚠️ **QUY CHUẨN KIẾN TRÚC MỚI TOÀN SÀN (CẬP NHẬT 30/10/2026):**  
> Toàn bộ hệ thống Project Nexus **ĐÃ BỎ HOÀN TOÀN GIAO TIẾP BẤT ĐỒNG BỘ** (RabbitMQ, MassTransit Broker, Transactional Outbox Pattern liên-service).  
> Mọi tương tác giữa các Microservices chuyển đổi 100% sang:
> 1. **Giao tiếp đồng bộ qua RESTful HTTP/JSON** (`HttpClientFactory` kèm cơ chế phục hồi Timeout / Retry với Polly).
> 2. **Xử lý tác vụ nền nội bộ (In-Memory Processing)** qua `System.Threading.Channels` + `BackgroundService` cho các tác vụ cần phản hồi nhanh (như gửi email OTP, dọn cache).

---

## 🎯 Khi Nào Kỹ Năng Này Được Kích Hoạt?

Bất cứ khi nào:
1. Bạn yêu cầu: `@outbox-event`, `gọi liên service`, `gửi OTP không cần RabbitMQ`, `đồng bộ hóa dữ liệu sang Inventory/Notification`, `chuyển đổi code bất đồng bộ sang REST`.
2. Kiểm tra các luồng nghiệp vụ trước đây dùng RabbitMQ (`AUTH` gửi OTP email, `SELLER` duyệt shop tạo kho ở Inventory...).
3. Đối soát và dọn dẹp các tàn dư của `IPublishEndpoint`, `MassTransit`, bảng Outbox khi codebase mới sẵn sàng.

---

## 🌐 1. QUY CHUẨN GIAO TIẾP LIÊN-SERVICE ĐỒNG BỘ (HTTP REST)

Khi một hành động tại `UserService` cần thông báo hoặc kích hoạt xử lý ở Microservice khác (ví dụ: Staff duyệt Seller $\rightarrow$ Tạo kho bên `InventoryService`):

### 1.1 Nguyên tắc thiết kế:
* **Không làm nghẽn luồng chính:** Tác vụ phụ trợ không được làm đổ vỡ thao tác chính của người dùng nếu service đối tác tạm thời gián đoạn.
* **Resilience Policy:** Bắt buộc áp dụng Timeout ngắn (tối đa 3s - 5s) và Retry tối đa 2 lần để tránh tích tụ kết nối gây sập hệ thống (Cascading Failure).
* **Đúng tầng Clean Architecture:**
  * `User.Application`: Khai báo Interface Service Client (ví dụ `IInventoryServiceClient`).
  * `User.Infrastructure`: Triển khai gọi HTTP qua `HttpClientFactory`.

### 1.2 Mẫu Code Client Đồng Bộ Chuẩn:
```csharp
namespace User.Infrastructure.Services.External;

using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using User.Application.Common.Interfaces;

public class InventoryServiceClient : IInventoryServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InventoryServiceClient> _logger;

    public InventoryServiceClient(HttpClient httpClient, ILogger<InventoryServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> CreateWarehouseAsync(Guid sellerId, Guid userId, string shopName, string pickupAddress, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                seller_id = sellerId,
                user_id = userId,
                shop_name = shopName,
                address = pickupAddress
            };

            var response = await _httpClient.PostAsJsonAsync("/api/v1/internal/warehouses", payload, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call InventoryService to create warehouse for Seller '{SellerId}'.", sellerId);
            return false;
        }
    }
}
```

---

## ⚡ 2. XỬ LÝ NỀN NỘI BỘ (IN-MEMORY BACKGROUND PROCESSING)

Đối với các tác vụ không cần người dùng phải chờ đợi như **gửi email mã xác thực OTP**, nhưng hệ thống không còn RabbitMQ/Notification Worker bên ngoài:

```
┌───────────────────────────────┐
│ 1. API tiếp nhận request      │
│ (Lưu DB + Redis mã OTP)       │
└──────────────┬────────────────┘
               ▼
┌───────────────────────────────┐
│ 2. Đẩy Job vào Channel<T>     │ ◄── Ghi vào bộ nhớ RAM tức thì (<1ms)
└──────────────┬────────────────┘
               ▼
┌───────────────────────────────┐
│ 3. API trả về ngay 200 OK     │ ◄── Client không bị đơ chờ gửi mail
└───────────────────────────────┘
               │ (Chạy ngầm song song)
               ▼
┌───────────────────────────────┐
│ 4. BackgroundService Worker   │ ◄── Rút Job từ Channel và gửi SMTP
└───────────────────────────────┘
```

---

## 🗄️ 3. HIỆN TRẠNG DỌN DẸP TÀN DƯ (LEGACY CLEANUP STATUS - ĐÃ HOÀN TẤT)

> ✅ **XÁC NHẬN TRIỂN KHAI (30/10/2026):** Toàn bộ tàn dư RabbitMQ và MassTransit **ĐÃ ĐƯỢC DỌN DẸP 100%** trên nhánh chuẩn (`refactor-all-src` / `dev-refactor`):

1. **`User.Infrastructure/DependencyInjection.cs`:** Đã gỡ bỏ toàn bộ `AddMassTransit` và `AddEntityFrameworkOutbox`. Thay bằng đăng ký `HttpClient` cho `IEmailNotificationClient`.
2. **`User.Application/Contracts/`:** Đã xóa bỏ toàn bộ các event cũ `SendOtpEmailIntegrationEvent.cs` và `SellerApprovedIntegrationEvent.cs`.
3. **`AuthService.cs` & `SellerService.cs`:** Đã thay thế hoàn toàn `IPublishEndpoint` bằng `IEmailNotificationClient` gọi REST API đồng bộ sang NotificationService.
4. **`AppDbContext.cs` & Migrations:** Đã xóa sạch 3 thực thể Outbox/Inbox (`InboxState`, `OutboxMessage`, `OutboxState`) khỏi model DbContext và file migration chuẩn `20261030054250_InitialCreate.cs`.
5. **Cấu hình môi trường:** Đã loại bỏ hoàn toàn các biến môi trường `RabbitMq:*` trong `docker-compose.yml` và `.env.example`.

---

## 🔍 4. CHECKLIST ĐỐI SOÁT KIẾN TRÚC MỚI (100% REST & IN-MEMORY)

Trước khi hoàn tất ca code có tương tác liên-service hoặc xử lý tác vụ nền:
- [ ] Không thêm mới bất kỳ cấu hình `MassTransit` hay lệnh `_publishEndpoint.Publish(...)` nào.
- [ ] Giao tiếp liên-service được đóng gói qua Interface trong tầng Application và triển khai bằng `IHttpClientFactory` trong Infrastructure.
- [ ] Cuộc gọi HTTP liên-service có timeout ngắn ($\le 5\text{s}$) và bọc `try/catch` có logging thích hợp, không làm hỏng transaction chính của UserService.
- [ ] Không ghi plain-text password hoặc OTP ra log môi trường production.
- [ ] Tác vụ gửi email OTP nếu chạy nội bộ phải đi qua hàng đợi RAM (`Channel<T>`) hoặc Mock Logger trong môi trường Dev, không chặn request HTTP của Client.
