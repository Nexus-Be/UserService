# [PUT] /api/v1/admin/configs

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `AdminConfig`  
> **Mã tham chiếu Excel:** `58`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `✅ VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Cập nhật giá trị một tham số cấu hình hệ thống động theo `key`, kiểm tra kiểu dữ liệu (`Int`, `Boolean`), ghi `AuditLog` (`AuditAction.SystemConfigUpdate`) và tự động xóa cache `IMemoryCache` (`sys_config:{key}`).
* **HTTP Method:** `PUT`
* **Đường dẫn chuẩn:** `/api/v1/admin/configs`
* **Quyền hạn truy cập (RBAC):** `Admin` (`[RequireRole("admin")]`)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/AdminConfigsController.cs` $\rightarrow$ `UpdateConfig()`
* **Application Interface:** `User.Application/Services/AdminConfig/IAdminConfigService.cs` $\rightarrow$ `UpdateConfigAsync()`
* **Service Implementation:** `User.Application/Services/AdminConfig/AdminConfigService.cs` $\rightarrow$ `UpdateConfigAsync()`
* **Request / Response DTOs:** `User.Application/Services/AdminConfig/AdminConfigRequests.cs` (`UpdateSystemConfigRequest`), `AdminConfigResponses.cs` (`UpdateSystemConfigResponse`)
* **Thực thể & Bảng DB:** `SystemConfig`, `AuditLog`

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Request Body (JSON Payload)
```json
{
  "key": "AUCTION_MIN_REPUTATION_BID",
  "value": "45",
  "reason": "Tighten auction bidding threshold during peak event."
}
```

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "key": "AUCTION_MIN_REPUTATION_BID",
  "oldValue": "40",
  "newValue": "45",
  "updatedAt": "2026-11-07T03:00:00Z"
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `400` | `VAL_INVALID_INPUT` | "Configuration key and value are required." | Thiếu `key` hoặc `value`. |
| `400` | `INVALID_CONFIG_VALUE` | "Configuration parameter value must be a valid integer." | Giá trị không khớp với `ConfigDataType` (`Int` hoặc `Boolean`). |
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Chưa đăng nhập. |
| `403` | `FORBIDDEN` | "You do not have permission to perform this action." | Không có quyền `admin`. |
| `404` | `CONFIG_NOT_FOUND` | "Configuration key was not found." | Không tìm thấy `ConfigKey` trong bảng `SystemConfigs`. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IAdminConfigService.cs` và `AdminConfigService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `AdminConfigsController.cs` (`PUT /api/v1/admin/configs`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test` vượt qua 100% (`AdminConfigAndSellerScoreInitTests.cs`).

