# [GET] /api/v1/users/{userId}/reputation/history

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Reputation`  
> **Mã tham chiếu Excel:** `23`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `✅ VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Xem lịch sử biến động điểm uy tín (cộng/trừ điểm theo thời gian) của một tài khoản. Chỉ chính chủ (`requesterId == userId`) hoặc `Admin`/`Staff` mới được phép truy cập.
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/users/{userId}/reputation/history`
* **Quyền hạn truy cập (RBAC):** `Authenticated` (Chính chủ hoặc `Admin`/`Staff`)
* **Yêu cầu Idempotency:** Không áp dụng (Idempotent theo HTTP spec)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/UsersController.cs` $\rightarrow$ `GetUserReputationHistory()`
* **Application Interface:** `User.Application/Services/Reputation/IReputationService.cs` $\rightarrow$ `GetUserReputationHistoryAsync()`
* **Service Implementation:** `User.Application/Services/Reputation/ReputationService.cs` $\rightarrow$ `GetUserReputationHistoryAsync()`
* **Item DTO:** `User.Application/Services/Reputation/ReputationDtos.cs` $\rightarrow$ `ReputationHistoryItemDto`
* **Thực thể & Bảng DB:** `User.Domain/Entities/ReputationLog.cs` $\rightarrow$ Bảng `ReputationLogs` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters
| Tên tham số | Kiểu dữ liệu | Bắt buộc? | Ràng buộc & Định dạng |
|:---|:---|:---:|:---|
| `userId` | `Guid (UUID)` | Có | Mã định danh của người dùng cần tra cứu |

### 3.2. Query Parameters
| Tên tham số | Kiểu dữ liệu | Mặc định | Ràng buộc & Khoảng giá trị |
|:---|:---|:---:|:---|
| `page` | `integer` | `1` | Số trang (tối thiểu 1) |
| `pageSize` | `integer` | `20` | Kích thước trang ($1 \le pageSize \le 100$) |
| `targetRole` | `TargetRole?` | `null` | Lọc theo quỹ điểm: `Buyer` hoặc `Seller` |

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "items": [
    {
      "logId": "30000000-aaaa-bbbb-cccc-000000000001",
      "targetRole": "Seller",
      "changeAmount": -15,
      "scoreAfter": 35,
      "reason": "ManualAdjustment",
      "referenceType": "System",
      "referenceId": null,
      "note": "Confirmed seller cancellation of active auction with bids.",
      "createdAt": "2026-11-07T03:00:00Z"
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
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Người dùng chưa đăng nhập. |
| `403` | `FORBIDDEN` | "You do not have permission to view another user's reputation history." | Người gọi không phải chính chủ và không có quyền `admin`/`staff`. |
| `404` | `USER_NOT_FOUND` | "User account was not found for the specified identifier." | Không tìm thấy người dùng theo `userId`. |

---

## 6. DANH MỤC ENUM LIÊN QUAN (DOMAIN ENUMS)

| Tên trường Payload (JSON Field) | Kiểu C# Enum | Giá trị DB (`integer`) | Giá trị API (`string PascalCase`) | Ý nghĩa nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `targetRole` | `TargetRole` | `0`, `1` | `Buyer`, `Seller` | Quỹ điểm bị thay đổi. |
| `reason` | `ReputationChangeReason` | `0`..`8` | `PositiveReview`, `NegativeReview`, `OrderCompleted`, `AuctionPaymentTimeout`, `DisputeCompensation`, `ManualAdjustment`, `ViolationPenalty`, `SellerCancellation`, `ReportPenalty` | Lý do biến động điểm uy tín. |

---

## 7. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IReputationService.cs` và `ReputationService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `UsersController.cs` (`GET /api/v1/users/{userId}/reputation/history`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test` vượt qua 100% (`ReputationServiceTests.cs`).

