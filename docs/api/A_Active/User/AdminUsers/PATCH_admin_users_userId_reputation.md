# [PATCH] /api/v1/admin/users/{userId}/reputation

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Reputation`  
> **Mã tham chiếu Excel:** `52`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `✅ VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Admin điều chỉnh thủ công điểm uy tín trên quỹ điểm `Buyer` (`CurrentScore`) hoặc `Seller` (`SellerScore`) của một người dùng, kèm tự động ghi `ReputationLog` (`ManualAdjustment`) và `AuditLog` (`AuditAction.ReputationAdjust`).
* **HTTP Method:** `PATCH`
* **Đường dẫn chuẩn:** `/api/v1/admin/users/{userId}/reputation`
* **Quyền hạn truy cập (RBAC):** `Admin` (`[RequireRole("admin")]`)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/AdminUsersController.cs` $\rightarrow$ `AdjustUserReputation()`
* **Application Interface:** `User.Application/Services/Reputation/IReputationService.cs` $\rightarrow$ `AdjustReputationAsync()`
* **Service Implementation:** `User.Application/Services/Reputation/ReputationService.cs` $\rightarrow$ `AdjustReputationAsync()`
* **Request / Response DTOs:** `User.Application/Services/Reputation/ReputationRequests.cs` (`AdjustReputationRequest`), `ReputationResponses.cs` (`AdjustReputationResponse`)
* **Thực thể & Bảng DB:** `ReputationScore`, `ReputationLog`, `AuditLog`

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters
| Tên tham số | Kiểu dữ liệu | Bắt buộc? | Ràng buộc & Định dạng |
|:---|:---|:---:|:---|
| `userId` | `Guid (UUID)` | Có | Mã định danh của người dùng cần điều chỉnh điểm |

### 3.2. Request Body (JSON Payload — hỗ trợ cả `scoreDelta` và `delta`)
```json
{
  "targetRole": "Seller",
  "scoreDelta": -15,
  "reason": "Confirmed seller cancellation of active auction with bids.",
  "referenceType": "Auction",
  "referenceId": "AUC-2026-001"
}
```

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "userId": "cccccccc-3333-3333-3333-333333333333",
  "targetRole": "Seller",
  "previousScore": 50,
  "newScore": 35,
  "appliedDelta": -15,
  "previousTrustLevel": "Standard",
  "newTrustLevel": "Untrusted",
  "reason": "Confirmed seller cancellation of active auction with bids.",
  "adjustedBy": "aaaaaaaa-1111-1111-1111-111111111111",
  "adjustedAt": "2026-11-07T03:00:00Z"
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `400` | `VAL_INVALID_INPUT` | "Score delta must be a non-zero integer between -50 and 50." | `ScoreDelta == 0` hoặc nằm ngoài khoảng `[-50, 50]`. |
| `400` | `VAL_INVALID_INPUT` | "Adjustment reason must be at least 10 characters long." | Lý do điều chỉnh ngắn hơn 10 ký tự. |
| `400` | `USER_NOT_A_SELLER` | "User account does not have an approved or suspended seller profile." | Khi `targetRole == TargetRole.Seller` nhưng người dùng không có hồ sơ Shop (`Approved` hoặc `Suspended`) hoặc vai trò `Seller`. |
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Chưa đăng nhập. |
| `403` | `FORBIDDEN` | "You do not have permission to perform this action." | Không có quyền `admin`. |
| `404` | `USER_NOT_FOUND` | "User account was not found for the specified identifier." | Không tìm thấy người dùng theo `userId`. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IReputationService.cs` và `ReputationService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `AdminUsersController.cs` (`PATCH /api/v1/admin/users/{userId}/reputation`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test` vượt qua 100% (`ReputationServiceTests.cs`).

