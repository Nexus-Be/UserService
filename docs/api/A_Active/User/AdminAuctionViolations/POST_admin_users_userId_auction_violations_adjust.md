# USER SERVICE - POST /api/v1/admin/users/{userId}/auction-violations/adjust

> **Feature / Use Case:** AdminAdjustAuctionViolations (#22)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-14
> **Tác giả:** Hiển Vinh & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/admin/users/{userId}/auction-violations/adjust`
- **Quyền hạn (RBAC Policy):** Chỉ `Admin` (`[RequireRole("admin")]`, `Staff` không có quyền)
- **Controller:** `User.Api/Controllers/Public/AdminAuctionViolationsController.cs` (`AdjustAuctionViolation`)
- **Application Contract:** `User.Application/Services/AuctionViolations/IAuctionViolationAdminService.cs` (`AdjustAuctionViolationAsync`)

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `userId` | Route | `Guid` | Có | UUID format | Định danh Buyer cần điều chỉnh |

### 2.2 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/AuctionViolations/AuctionViolationAdminRequests.cs`
- **Tên C# Record:** `AdjustAuctionViolationRequest`
- **JSON Request Body mẫu (camelCase):**
```json
{
  "action": "ResetStreak",
  "value": null,
  "until": null,
  "reason": "Valid dispute approved due to payment gateway outage."
}
```

| `action` (PascalCase) | `value` | `until` | Hành vi |
| :--- | :--- | :--- | :--- |
| `ResetStreak` | — | — | Đặt `ViolationStreak = 0` |
| `SetStreak` | `>= 0` (bắt buộc) | — | Đặt `ViolationStreak = value` |
| `ApplyRestriction` | — | `> UtcNow` (bắt buộc) | Áp `TempRestricted` đến `until` |
| `LiftRestriction` | — | — | Gỡ hạn chế (`None`, `until = null`) |
| `Suspend` | — | tùy chọn (`> UtcNow` nếu có) | Áp `Suspended` (vô thời hạn nếu `until = null`) |

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `User.Application/Services/AuctionViolations/AuctionViolationAdminResponses.cs`
- **Tên C# Record:** `AdjustAuctionViolationResponse`
- **Mã HTTP thành công:** `200 OK`
- **JSON Response Body mẫu (camelCase):**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "before": {
    "streak": 3,
    "restriction": "TempRestricted",
    "restrictionUntil": "2026-11-20T10:00:00Z"
  },
  "after": {
    "streak": 0,
    "restriction": null,
    "restrictionUntil": null
  },
  "auditId": "99999999-8888-7777-6666-555555555555"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `One or more validation errors occurred.` | Thiếu `reason`, `value < 0`, hoặc `until` ở quá khứ |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Thiếu hoặc token không hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Người gọi không phải `admin` |
| `404 Not Found` | `USER_NOT_FOUND` | `User account was not found.` | Không tìm thấy người dùng |
| `409 Conflict` | `NOTHING_TO_CHANGE` | `No auction violation state changes are required.` | Trạng thái mới trùng trạng thái hiện tại |
| `409 Conflict` | `CANNOT_RESTRICT_ADMIN` | `Cannot modify auction restrictions for an admin account.` | Tài khoản đích có vai trò `admin` |
