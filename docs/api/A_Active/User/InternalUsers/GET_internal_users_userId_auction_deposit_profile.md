# USER SERVICE - GET /api/v1/internal/users/{userId}/auction-deposit-profile

> **Feature / Use Case:** InternalAuctionDepositProfile (#19)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-14
> **Tác giả:** Hiển Vinh & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/internal/users/{userId}/auction-deposit-profile`
- **Quyền hạn (RBAC Policy):** Internal Service-to-Service (`AllowAnonymous` trên controller hiện tại; internal key check đang tắt theo `AGENTS.md` §4.10)
- **Controller:** `User.Api/Controllers/Internal/InternalUsersController.cs` (`GetAuctionDepositProfile`)
- **Application Contract:** `User.Application/Services/Internal/IInternalUserService.cs` (`GetAuctionDepositProfileAsync`)

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `userId` | Route | `Guid` | Có | UUID format | Định danh Buyer cần kiểm tra hồ sơ đấu giá |

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService trả về DTO thuần (object trần), enum tuần tự hóa `PascalCase`.

- **Vị trí file:** `User.Application/Services/Internal/InternalUserResponses.cs`
- **Tên C# Record:** `AuctionDepositProfileResponse`
- **Mã HTTP thành công:** `200 OK`
- **JSON Response Body mẫu (camelCase):**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "accountStatus": "Active",
  "reputationScore": 90,
  "isNewUser": false,
  "violationStreak": 1,
  "auctionRestriction": {
    "isRestricted": false,
    "type": null,
    "until": null,
    "reason": null
  }
}
```

### 3.1 Quy tắc nghiệp vụ
- Truy vấn bằng `.IgnoreQueryFilters()`: nếu `IsDeleted || Status == UserStatus.Deleted` thì trả `200 OK` với `accountStatus = "Deleted"`.
- Nếu `RestrictionUntil <= DateTimeOffset.UtcNow`, tự động coi là hết hạn (`isRestricted = false`, `type = null`, `until = null`, `reason = null`).
- Hạn chế đấu giá chỉ áp dụng cho đấu giá, **không** ảnh hưởng `User.CanPlaceOrder()` (mua hàng thường).

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `One or more validation errors occurred.` | `userId` không đúng định dạng GUID |
| `404 Not Found` | `USER_NOT_FOUND` | `User account was not found.` | Không tìm thấy `userId` trong DB |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống chưa được xử lý |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `UserStatus.PendingVerification` | `0` | `"PendingVerification"` | Tài khoản chưa xác thực OTP |
| `UserStatus.Active` | `1` | `"Active"` | Tài khoản đang hoạt động |
| `UserStatus.Banned` | `2` | `"Banned"` | Tài khoản bị khóa |
| `UserStatus.Deleted` | `3` | `"Deleted"` | Tài khoản đã xóa mềm |
| `AuctionRestrictionType.TempRestricted` | `1` | `"TempRestricted"` | Hạn chế đấu giá tạm thời |
| `AuctionRestrictionType.Suspended` | `2` | `"Suspended"` | Đình chỉ đấu giá |
