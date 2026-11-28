# [GET] /api/v1/users/{userId}/reputation

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Reputation`  
> **Mã tham chiếu Excel:** `22`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `✅ VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Xem thẻ tóm tắt hồ sơ uy tín công khai của một người dùng bất kỳ (bao gồm cả điểm Buyer, điểm Seller và danh sách huy hiệu `Badges`).
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/users/{userId}/reputation`
* **Quyền hạn truy cập (RBAC):** `AllowAnonymous` (Công khai)
* **Yêu cầu Idempotency:** Không áp dụng (Idempotent theo HTTP spec)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/UsersController.cs` $\rightarrow$ `GetUserReputation()`
* **Application Interface:** `User.Application/Services/Reputation/IReputationService.cs` $\rightarrow$ `GetUserReputationSummaryAsync()`
* **Service Implementation:** `User.Application/Services/Reputation/ReputationService.cs` $\rightarrow$ `GetUserReputationSummaryAsync()`
* **Response DTO:** `User.Application/Services/Reputation/ReputationResponses.cs` $\rightarrow$ `PublicReputationResponse`
* **Thực thể & Bảng DB:** `User.Domain/Entities/ReputationScore.cs` $\rightarrow$ Bảng `ReputationScores` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters
| Tên tham số | Kiểu dữ liệu | Bắt buộc? | Ràng buộc & Định dạng |
|:---|:---|:---:|:---|
| `userId` | `Guid (UUID)` | Có | Mã định danh duy nhất của người dùng |

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "userId": "cccccccc-3333-3333-3333-333333333333",
  "currentScore": 85,
  "trustLevel": "Good",
  "totalPositiveReviews": 10,
  "totalNegativeReviews": 0,
  "totalReviews": 25,
  "positiveRatio": 100.00,
  "sellerScore": 85,
  "sellerTrustLevel": "Good",
  "sellerPositiveReviews": 15,
  "sellerNegativeReviews": 0,
  "badges": [
    "TrustedBuyer",
    "VerifiedSeller",
    "TrustedSeller"
  ],
  "memberSince": "2026-01-01T00:00:00Z"
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `404` | `USER_NOT_FOUND` | "User account was not found for the specified identifier." | Không tìm thấy người dùng theo `userId`. |
| `500` | `INTERNAL_SERVER_ERROR` | "An unexpected error occurred. Please try again later." | Lỗi hệ thống nội bộ chưa được xử lý. |

---

## 6. DANH MỤC ENUM LIÊN QUAN (DOMAIN ENUMS)

| Tên trường Payload (JSON Field) | Kiểu C# Enum | Giá trị DB (`integer`) | Giá trị API (`string PascalCase`) | Ý nghĩa nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `trustLevel` / `sellerTrustLevel` | `TrustLevel` | `0`..`4` | `Untrusted`, `Probation`, `Standard`, `Good`, `Excellent` | Phân hạng tín nhiệm Buyer và Seller. |

---

## 7. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IReputationService.cs` và `ReputationService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `UsersController.cs` (`GET /api/v1/users/{userId}/reputation`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test` vượt qua 100% (`ReputationServiceTests.cs`).

