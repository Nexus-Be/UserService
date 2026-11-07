# [GET] /api/v1/me/reputation

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Reputation`  
> **Mã tham chiếu Excel:** `17`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `✅ VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Xem chi tiết điểm uy tín kép (`CurrentScore` cho vai trò Buyer và `SellerScore` độc lập cho vai trò Seller) của chính người dùng đang đăng nhập.
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/me/reputation`
* **Đường dẫn gốc trong Excel:** `/api/v1/me/reputation` (Contextual Singleton giữ nguyên theo chuẩn RFC 9110.)
* **Quyền hạn truy cập (RBAC):** `Authenticated` (`[RequireRole]`)
* **Yêu cầu Idempotency:** Không áp dụng (Idempotent theo HTTP spec)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/MeController.cs` $\rightarrow$ `GetMyReputation()`
* **Application Interface:** `User.Application/Services/Reputation/IReputationService.cs` $\rightarrow$ `GetMyReputationAsync()`
* **Service Implementation:** `User.Application/Services/Reputation/ReputationService.cs` $\rightarrow$ `GetMyReputationAsync()`
* **Response DTO:** `User.Application/Services/Reputation/ReputationResponses.cs` $\rightarrow$ `MyReputationResponse`
* **Thực thể & Bảng DB:** `User.Domain/Entities/ReputationScore.cs` $\rightarrow$ Bảng `ReputationScores` và `UserPenalties` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters (Tham số đường dẫn)
*Không có tham số đường dẫn.*

### 3.2. Query Parameters (Tham số truy vấn)
*Không có tham số truy vấn.*

### 3.3. Request Headers
```json
{
  "Authorization": "Bearer <JWT>",
  "X-User-Id": "uuid"
}
```

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "currentScore": 75,
  "trustLevel": "Good",
  "canBid": true,
  "totalPositiveReviews": 15,
  "totalNegativeReviews": 0,
  "penaltyCount": 0,
  "nextLevelThreshold": 90,
  "sellerScore": 50,
  "sellerTrustLevel": "Standard",
  "canSell": true,
  "sellerPositiveReviews": 0,
  "sellerNegativeReviews": 0,
  "sellerPenaltyCount": 0,
  "sellerNextLevelThreshold": 70,
  "updatedAt": "2026-11-07T03:00:00Z"
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Thiếu header `X-User-Id` hoặc JWT không hợp lệ. |
| `404` | `USER_NOT_FOUND` | "User account was not found for the specified identifier." | Không tìm thấy tài khoản người dùng trong bảng `Users`. |
| `500` | `INTERNAL_SERVER_ERROR` | "An unexpected error occurred. Please try again later." | Lỗi hệ thống nội bộ chưa được xử lý. |

---

## 6. DANH MỤC ENUM LIÊN QUAN (DOMAIN ENUMS)

| Tên trường Payload (JSON Field) | Kiểu C# Enum | Giá trị DB (`integer`) | Giá trị API (`string PascalCase`) | Ý nghĩa nghiệp vụ & Hướng dẫn hiển thị Frontend |
|:---|:---|:---:|:---|:---|
| `trustLevel` / `sellerTrustLevel` | `TrustLevel` | `0` | `Untrusted` | **Không đáng tin cậy** (`0..39` điểm): Bị chặn cả đấu giá và bán hàng. Badge đỏ (Danger). |
| `trustLevel` / `sellerTrustLevel` | `TrustLevel` | `1` | `Probation` | **Thử thách** (`40..49` điểm): Được phép đặt giá mua (`>= 40`), nhưng bị chặn mở phiên bán (`< 50`). Badge cam (Warning). |
| `trustLevel` / `sellerTrustLevel` | `TrustLevel` | `2` | `Standard` | **Tiêu chuẩn** (`50..69` điểm): Điểm khởi tạo mặc định (`50`), đầy đủ quyền mua và bán. Badge xanh dương (Info). |
| `trustLevel` / `sellerTrustLevel` | `TrustLevel` | `3` | `Good` | **Tốt** (`70..89` điểm): Uy tín tốt, nhận huy hiệu Trusted. Badge xanh lá (Success). |
| `trustLevel` / `sellerTrustLevel` | `TrustLevel` | `4` | `Excellent` | **Xuất sắc** (`90..100` điểm): Uy tín cao nhất toàn sàn. Badge vàng kim (Gold/Vip). |

---

## 7. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IReputationService.cs` và `ReputationService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `MeController.cs` (`GET /api/v1/me/reputation`) đã triển khai chuẩn.
- [x] **3. Bắt lỗi & Validation:** Toàn bộ mã lỗi trong ma trận ở Mục 5 đã được kiểm soát trong code.
- [x] **4. Enum Mapping:** Toàn bộ enum ở Mục 6 được lưu integer trong DB và serialize PascalCase ở API.
- [x] **5. Kiểm thử tự động:** `dotnet test` vượt qua 100% (`ReputationServiceTests.cs`, `ReputationAndGovernanceControllersTests.cs`).

