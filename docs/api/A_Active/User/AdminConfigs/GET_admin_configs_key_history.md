# [GET] /api/v1/admin/configs/{key}/history

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `AdminConfig`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Admin tra cứu lịch sử thay đổi giá trị của một tham số cấu hình hệ thống theo `key` (ai đã đổi, đổi lúc nào, từ giá trị cũ sang giá trị mới và lý do).
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/admin/configs/{key}/history`
* **Quyền hạn truy cập (RBAC):** `Admin` (`[RequireRole("admin")]`)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/AdminConfigsController.cs` $\rightarrow$ `GetConfigHistory()`
* **Application Interface:** `User.Application/Services/AdminConfig/IAdminConfigService.cs` $\rightarrow$ `GetConfigHistoryAsync()`
* **Service Implementation:** `User.Application/Services/AdminConfig/AdminConfigService.cs` $\rightarrow$ `GetConfigHistoryAsync()`
* **Item DTO:** `User.Application/Services/AdminConfig/AdminConfigDtos.cs` $\rightarrow$ `SystemConfigHistoryItemDto`
* **Thực thể & Bảng DB:** `User.Domain/Entities/AuditLog.cs` / `SystemConfig` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route & Query Parameters
| Tên tham số | Vị trí | Kiểu dữ liệu | Mặc định | Mô tả |
|:---|:---|:---|:---:|:---|
| `key` | Route | `string` | Bắt buộc | Khóa cấu hình cần xem lịch sử (ví dụ: `minReputationToBid`). |
| `page` | Query | `integer` | `1` | Số trang (tối thiểu 1). |
| `pageSize` | Query | `integer` | `20` | Kích thước trang ($1 \le pageSize \le 100$). |

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "items": [
    {
      "id": "80000000-aaaa-bbbb-cccc-000000000001",
      "changedBy": "aaaaaaaa-1111-1111-1111-111111111111",
      "oldValue": "40",
      "newValue": "45",
      "reason": "Tighten bidding eligibility threshold",
      "changedAt": "2026-11-12T10:00:00Z"
    }
  ],
  "pagination": {
    "currentPage": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1,
    "hasPrevious": false,
    "hasNext": false
  }
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Thiếu hoặc token không hợp lệ. |
| `403` | `FORBIDDEN` | "You do not have permission to access this resource." | Người dùng không có vai trò `admin`. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IAdminConfigService.cs` và `AdminConfigService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `AdminConfigsController.cs` (`GET /api/v1/admin/configs/{key}/history`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test User.sln -c Release` vượt qua 100%.
