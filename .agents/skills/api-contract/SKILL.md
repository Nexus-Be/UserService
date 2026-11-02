---
name: api-contract
description: >-
  Automated API contract alignment and verification skill between ApiGateway and UserService backend: compares route paths, HTTP verbs, request/response DTO JSON properties, header requirements, and SCREAMING_SNAKE_CASE error codes to guarantee 100% interoperability without runtime integration errors.
---

# API Contract Skill (ApiGateway ↔ UserService Alignment)

Kỹ năng chuyên biệt dùng để **đối soát tự động 100% hợp đồng API** giữa **ApiGateway** và **UserService**, đảm bảo khi Gateway chuyển tiếp request xuống Backend là chạy trơn tru ngay lần đầu tiên, không bao giờ bị lỗi `400 Bad Request` hay lệch tên trường JSON.

---

## 🎯 Khi Nào Kỹ Năng Này Được Kích Hoạt?

Bất cứ khi nào:
1. Bạn yêu cầu: `đối soát API [tên_api]`, `kiểm tra khớp contract [chức_năng]`, `@api-contract [endpoint]`.
2. **Trước khi bắt đầu code một Controller / DTO mới bên UserService:** Tự động soi chiếu với Route YARP tương ứng trên Gateway (tại `C:\Users\ACER\Desktop\Gateway`) để đối soát route path và chuẩn DTO.
3. **Khi gặp lỗi gọi API qua Gateway thất bại (HTTP 400 / 404 / 500):** Tự động đối soát 2 đầu để tìm ra điểm lệch pha.

---

## 🔍 Nguồn Sự Thật Cần Mở Đối Chiếu (Source Files)

Khi thực hiện đối soát, AI Agent mở trực tiếp cấu hình YARP Gateway tại `C:\Users\ACER\Desktop\Gateway` để so khớp với 7 Controller tại `User.Api/Controllers/Public/`:

| Phân hệ API | YARP Route & Config trên Gateway | File Controller trên UserService |
| :--- | :--- | :--- |
| **Xác thực & Phiên (Auth)** | `auth-route`: `/api/v1/auth/{**catch-all}` (`Gateway/appsettings.json`) | `User.Api/Controllers/Public/AuthController.cs` |
| **Hồ sơ cá nhân (Me)** | `user-me-route`: `/api/v1/me/{**catch-all}` (`Gateway/appsettings.json`) | `User.Api/Controllers/Public/MeController.cs` |
| **Người dùng (Users)** | `users-route`: `/api/v1/users/{**catch-all}` (`Gateway/appsettings.json`) | `User.Api/Controllers/Public/UsersController.cs` |
| **Gian hàng & Người bán (Sellers)** | `sellers-route`: `/api/v1/sellers/{**catch-all}` (`Gateway/appsettings.json`) | `User.Api/Controllers/Public/SellerController.cs` |
| **Quản trị người dùng (Admin Users)**| `admin-users-route`: `/api/v1/admin/users/{**catch-all}` | `User.Api/Controllers/Public/AdminUsersController.cs` |
| **Quản trị vai trò (Admin Roles)** | `admin-roles-route`: `/api/v1/admin/roles/{**catch-all}` | `User.Api/Controllers/Public/AdminRolesController.cs` |
| **Duyệt hồ sơ bán (Admin Seller Reg)**| `admin-seller-registrations-route`: `/api/v1/admin/seller-registrations/{**catch-all}` | `User.Api/Controllers/Public/AdminSellerRegistrationsController.cs` |

> 📖 **Tài liệu đặc tả Gateway tham chiếu:**
> - Danh mục Route YARP: `C:\Users\ACER\Desktop\Gateway\specs\02_ROUTING_CATALOG.md`
> - Kiến trúc Pipeline & Envelope: `C:\Users\ACER\Desktop\Gateway\specs\01_ARCHITECTURE.md`
> - Anti-spoofing & Context: `C:\Users\ACER\Desktop\Gateway\Gateway\Middlewares\ClientContextMiddleware.cs`
> - Tiêm danh tính (Identity Propagation): `C:\Users\ACER\Desktop\Gateway\Gateway\Transforms\IdentityPropagationTransform.cs`

---

## 📐 Quy Trình Đối Soát 5 Trục Chuẩn (5-Axis Verification Checklist)

Mọi API khi đối soát bắt buộc phải vượt qua toàn bộ 5 trục kiểm tra sau:

### Trục 1: Tuyến đường (Route Path) & Phương thức HTTP (Verb)
- **Kiểm tra URL:** Đường dẫn downstream có khớp từng chữ không? (ví dụ: `/api/v1/auth/login` hay `/api/v1/auth/sign-in`).
- **HTTP Verb:** `GET` (đọc), `POST` (tạo mới/thao tác bảo mật), `PUT` (thay thế toàn bộ/duyệt hồ sơ), `PATCH` (cập nhật 1 phần/khóa tài khoản), `DELETE` (xóa mềm).
- **Hỗ trợ Alias:** Kiểm tra xem Controller có khai báo route rút gọn không (ví dụ: `[HttpGet("me")]` và `[HttpGet("/api/v1/me")]`).

### Trục 2: DTO Đầu vào & Quy cách đặt tên JSON (`JsonPropertyName`)
- **Quy tắc snake_case vs camelCase:** Gateway và Frontend thường gửi JSON định dạng `snake_case` (ví dụ `phone_number`, `rejection_reason`, `banned_until`) hoặc `camelCase` (`phoneNumber`, `rejectionReason`).
- **Bắt buộc gắn Annotation & Khuyến nghị Dual-Binding:** Phía UserService, các thuộc tính DTO C# viết `PascalCase` bắt buộc phải có annotation tương ứng để tránh bị nhận giá trị `null`:
  ```csharp
  // Cách 1: Gán trực tiếp snake_case chuẩn của hệ thống:
  [JsonPropertyName("phone_number")]
  public string? PhoneNumber { get; set; }

  // Cách 2: Kỹ thuật Dual-Binding (Khuyên dùng) - Hứng song song cả snake_case lẫn camelCase:
  [JsonPropertyName("shop_name")]
  public string? ShopNameSnake { set => ShopName ??= value; }

  [JsonPropertyName("shopName")]
  public string? ShopName { get; set; }
  ```
- **Response DTO (C# Record):** Sử dụng `[property: JsonPropertyName("fieldName")]` để cố định chuẩn đầu ra JSON.
- **Kiểu dữ liệu:** Đảm bảo kiểu dữ liệu khớp 100% (UUID/Guid, string, integer, boolean, DateTimeOffset).

### Trục 3: Khung Đóng Gói Đầu Ra (Response Structure & Envelope Separation)
- **Phân định rõ ràng trách nhiệm:**
  * **Phía UserService Backend:** **CHỈ trả về Raw DTO nghiệp vụ** (ví dụ `UserResponseDto`, `SellerPublicProfileResponseDto`, `TokenResultDto`). Không bọc thừa thãi envelope `ApiResponse` ở backend.
  * **Phía ApiGateway:** Tiếp nhận Raw DTO từ UserService và **tự động bọc vào Envelope chuẩn `ApiResponse<T>`** để trả về cho Frontend/Client:
    ```json
    {
      "success": true,
      "message": "Sentence case English message",
      "data": { ...raw DTO từ backend... }
    }
    ```
- Trạng thái HTTP thành công: `200 OK` (đọc/sửa), `201 Created` (tạo mới), `204 NoContent` (logout/xóa).

### Trục 4: Tiêu đề Ngữ cảnh (Header Propagation & HttpContextExtensions)
Kiểm tra UserService có đọc đúng các Header mà Gateway chuyển tiếp xuống không (được hỗ trợ tự động qua `User.Api/Extensions/HttpContextExtensions.cs`):
- `x-user-id`: Chứa UUID của người dùng hiện tại (tiêm an toàn bởi Gateway `IdentityPropagationTransform`).
- `x-user-roles`: Danh sách vai trò của người dùng chuyển tiếp từ Gateway (ví dụ `buyer,seller,admin`). *(Lưu ý: Hệ thống đã bãi bỏ RBAC/Permissions, phân quyền hoàn toàn qua vai trò).*
- `x-user-email`: Email của người dùng hiện tại.
- `x-correlation-id`: ID định danh luồng xử lý phục vụ Distributed Tracing (Tempo/Grafana).
- `X-Internal-Service-Key` / `X-Internal-Token`: Token bí mật giữa các services nội bộ (đối với route `/api/v1/internal/*`, Gateway tự động chặn 404 nếu client ngoài cố tình gọi vào).
- 🚨 **Active Anti-Spoofing:** Gateway (`ClientContextMiddleware`) sẽ lập tức trả về `403 Forbidden` (`FORBIDDEN_HEADER_SPOOFING`) nếu client bên ngoài tự ý gửi `x-user-id` hoặc `x-user-roles`.

### Trục 5: Đồng bộ Mã Lỗi & Thông báo (Error Codes & Messages)
- **Mã lỗi bắt buộc:** `SCREAMING_SNAKE_CASE` (ví dụ: `EMAIL_ALREADY_EXISTS`, `OTP_INVALID`, `USER_NOT_FOUND`, `VAL_INVALID_INPUT`).
- **Thông báo lỗi:** 100% tiếng Anh, chuẩn Sentence case (chỉ viết hoa chữ cái đầu câu: `"OTP code has expired or does not exist."`).

---

## 📊 Mẫu Báo Cáo Đối Soát Chuẩn (Contract Alignment Report)

Khi kích hoạt kỹ năng này, AI sẽ xuất ra bảng so sánh chi tiết:

```markdown
# 🔍 KẾT QUẢ ĐỐI SOÁT HỢP ĐỒNG API: [TÊN TÍNH NĂNG]
- **Endpoint:** `[METHOD] /api/v1/...`
- **Mã chức năng:** `[AUTH-xx / USER-xx / SELLER-xx / ADMIN-xx]`

| Hạng mục đối soát | Phía ApiGateway | Phía UserService Backend | Kết luận |
| :--- | :--- | :--- | :---: |
| **HTTP Verb & Route** | `POST /api/v1/auth/password/reset` | `PUT /api/v1/auth/password/reset` | ⚠️ Lệch Verb |
| **Request DTO** | `{ email, otp_code, new_password }` | `{ email, otp, new_password }` | ⚠️ Lệch field `otp` vs `otp_code` |
| **Response Format** | `{ success, message }` | `{ success: true }` | ✅ Khớp |
| **Header yêu cầu** | Không yêu cầu (Public) | Không yêu cầu | ✅ Khớp |
| **Error Codes** | `OTP_INVALID`, `OTP_EXPIRED` | `OTP_INVALID`, `OTP_EXPIRED` | ✅ Khớp 100% |

### 🛠️ Đề xuất chỉnh sửa mã nguồn:
[Cung cấp đoạn code DTO hoặc Controller chuẩn để sửa lỗi lệch pha]
```
