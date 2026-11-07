# [GET] /api/v1/me/penalties

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Reputation`  
> **Mã tham chiếu Excel:** `18`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `✅ VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Xem danh sách lịch sử các án phạt vi phạm của chính người dùng đang đăng nhập (kèm phân biệt vai trò bị phạt `TargetRole`: `Buyer`, `Seller`, `Account`).
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/me/penalties`
* **Quyền hạn truy cập (RBAC):** `Authenticated` (`[RequireRole]`)
* **Yêu cầu Idempotency:** Không áp dụng (Idempotent theo HTTP spec)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/MeController.cs` $\rightarrow$ `GetMyPenalties()`
* **Application Interface:** `User.Application/Services/Reputation/IReputationService.cs` $\rightarrow$ `GetMyPenaltiesAsync()`
* **Service Implementation:** `User.Application/Services/Reputation/ReputationService.cs` $\rightarrow$ `GetMyPenaltiesAsync()`
* **Item DTO:** `User.Application/Services/Reputation/ReputationDtos.cs` $\rightarrow$ `MyPenaltyItemDto`
* **Thực thể & Bảng DB:** `User.Domain/Entities/UserPenalty.cs` $\rightarrow$ Bảng `UserPenalties` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters
*Không có tham số đường dẫn.*

### 3.2. Query Parameters
| Tên tham số | Kiểu dữ liệu | Mặc định | Ràng buộc & Khoảng giá trị |
|:---|:---|:---:|:---|
| `page` | `integer` | `1` | Số trang (tối thiểu 1; nếu `< 1` tự chuẩn hóa về `1`) |
| `pageSize` | `integer` | `20` | Kích thước trang ($1 \le pageSize \le 100$; ngoài khoảng chuẩn hóa về `20`) |

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "items": [
    {
      "penaltyId": "60000000-aaaa-bbbb-cccc-000000000001",
      "targetRole": "Buyer",
      "violationType": "PaymentTimeout",
      "reason": "Buyer failed to pay winning auction invoice within 24 hours.",
      "penaltyApplied": "DeductPoints",
      "pointsDeducted": 10,
      "status": "Active",
      "policyClauseCode": "AUC_PAYMENT_24H",
      "issuedAt": "2026-11-07T03:00:00Z",
      "expiresAt": null,
      "isCurrentlyActive": true
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
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Thiếu header `X-User-Id` hoặc JWT không hợp lệ. |
| `500` | `INTERNAL_SERVER_ERROR` | "An unexpected error occurred. Please try again later." | Lỗi hệ thống nội bộ chưa được xử lý. |

---

## 6. DANH MỤC ENUM LIÊN QUAN (DOMAIN ENUMS)

| Tên trường Payload (JSON Field) | Kiểu C# Enum | Giá trị DB (`integer`) | Giá trị API (`string PascalCase`) | Ý nghĩa nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `targetRole` | `TargetRole` | `0`, `1`, `2` | `Buyer`, `Seller`, `Account` | Nhánh quyền hạn bị áp dụng chế tài. |
| `status` | `PenaltyStatus` | `0`, `1`, `2` | `Active`, `Expired`, `Revoked` | Trạng thái hiệu lực của án phạt. |
| `penaltyApplied` | `PenaltyType` | `0`..`4` | `Warning`, `DeductPoints`, `Restriction`, `BanAccount`, `Other` | Hình thức kỷ luật được áp dụng. |

---

## 7. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IReputationService.cs` và `ReputationService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `MeController.cs` (`GET /api/v1/me/penalties`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test` vượt qua 100% (`ReputationServiceTests.cs`).

