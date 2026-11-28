# [GET] /api/v1/admin/configs

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `AdminConfig`  
> **Mã tham chiếu Excel:** `57`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `✅ VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Xem danh sách tham số cấu hình hệ thống động toàn sàn (bao gồm các ngưỡng điểm uy tín, mức phạt tự động, thời gian chống cày điểm).
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/admin/configs`
* **Quyền hạn truy cập (RBAC):** `Admin` (`[RequireRole("admin")]`)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/AdminConfigsController.cs` $\rightarrow$ `GetConfigs()`
* **Application Interface:** `User.Application/Services/AdminConfig/IAdminConfigService.cs` $\rightarrow$ `GetConfigsAsync()`
* **Service Implementation:** `User.Application/Services/AdminConfig/AdminConfigService.cs` $\rightarrow$ `GetConfigsAsync()`
* **Item DTO:** `User.Application/Services/AdminConfig/AdminConfigDtos.cs` $\rightarrow$ `SystemConfigItemDto`
* **Thực thể & Bảng DB:** `User.Domain/Entities/SystemConfig.cs` $\rightarrow$ Bảng `SystemConfigs` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Query Parameters
| Tên tham số | Kiểu dữ liệu | Mặc định | Ràng buộc & Khoảng giá trị |
|:---|:---|:---:|:---|
| `page` | `integer` | `1` | Số trang (tối thiểu 1) |
| `pageSize` | `integer` | `20` | Kích thước trang ($1 \le pageSize \le 100$) |

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "items": [
    {
      "key": "AUCTION_MIN_REPUTATION_BID",
      "value": "40",
      "description": "Minimum buyer reputation score required to place a bid in an auction.",
      "dataType": "Int",
      "updatedAt": "2026-10-30T00:00:00Z"
    }
  ],
  "pagination": {
    "currentPage": 1,
    "pageSize": 20,
    "totalCount": 9,
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
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Chưa đăng nhập. |
| `403` | `FORBIDDEN` | "You do not have permission to perform this action." | Không có quyền `admin`. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IAdminConfigService.cs` và `AdminConfigService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `AdminConfigsController.cs` (`GET /api/v1/admin/configs`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test` vượt qua 100% (`AdminConfigAndSellerScoreInitTests.cs`).

