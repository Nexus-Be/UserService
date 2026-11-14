# USER SERVICE - POST /api/v1/internal/users/{userId}/auction-violations

> **Feature / Use Case:** InternalRecordAuctionViolation (#20)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-14
> **Tác giả:** Hiển Vinh & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/internal/users/{userId}/auction-violations`
- **Quyền hạn (RBAC Policy):** Internal Service-to-Service
- **Controller:** `User.Api/Controllers/Internal/InternalUsersController.cs` (`RecordAuctionViolation`)
- **Application Contract:** `User.Application/Services/Internal/IInternalUserService.cs` (`RecordAuctionViolationAsync`)

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `userId` | Route | `Guid` | Có | UUID format | Định danh Buyer vi phạm |

### 2.2 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Internal/InternalUserRequests.cs`
- **Tên C# Record:** `RecordAuctionViolationRequest`
- **JSON Request Body mẫu (camelCase):**
```json
{
  "violationType": "WinnerNonPayment",
  "auctionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "orderId": "8f14e45f-ceea-467a-9c56-9a3e2c112233",
  "occurredAt": "2026-11-13T10:00:01Z",
  "depositForfeitedAmount": 50000
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `User.Application/Services/Internal/InternalUserResponses.cs`
- **Tên C# Record:** `RecordAuctionViolationResponse`
- **Mã HTTP thành công:** `201 Created` (tạo mới) hoặc `200 OK` (đã tồn tại — idempotent trả lại snapshot lần đầu với `created = false`)
- **JSON Response Body mẫu (camelCase):**
```json
{
  "violationId": "11111111-2222-3333-4444-555555555555",
  "created": true,
  "previousStreak": 1,
  "newStreak": 2,
  "reputationBefore": 90,
  "reputationAfter": 80,
  "sanction": {
    "type": "DepositRateIncreased",
    "until": null
  }
}
```

### 3.1 Quy tắc xử phạt theo Streak (`AuctionRules`)
| Streak mới | Trừ điểm Buyer | Chế tài (`AuctionSanctionType`) | Thời hạn |
| :---: | :---: | :--- | :--- |
| `1` | `-5` | `Warning` | `null` |
| `2` | `-10` | `DepositRateIncreased` | `null` |
| `3` | `-15` | `TempRestricted` | `+7 ngày` (giữ nguyên nếu đang có hạn chế dài hơn) |
| `>= 4` | `-20` | `Suspended` | Vô thời hạn (`null`) |

- Idempotency đảm bảo bởi unique filtered index `IX_AuctionViolations_System_Idempotency` trên `(UserId, AuctionId, ViolationType)` với `Source = System`.

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Violation type is required.` / `Deposit forfeited amount must be non-negative.` / `Deposit forfeited amount must be 0 for second-chance non-payment.` | Thiếu trường bắt buộc, số tiền âm, hoặc `SecondChanceNonPayment` có tiền phạt `> 0` |
| `404 Not Found` | `USER_NOT_FOUND` | `User account was not found.` | Không tìm thấy người dùng |
| `422 Unprocessable Entity` | `INVALID_VIOLATION_TYPE` | `Invalid auction violation type.` | `violationType` không thuộc `AuctionViolationType` |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống chưa được xử lý |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `AuctionViolationType.WinnerNonPayment` | `0` | `"WinnerNonPayment"` | Người thắng không thanh toán |
| `AuctionViolationType.SecondChanceNonPayment` | `1` | `"SecondChanceNonPayment"` | Người về nhì nhận mua nhưng không thanh toán |
| `AuctionSanctionType.Warning` | `1` | `"Warning"` | Cảnh báo |
| `AuctionSanctionType.DepositRateIncreased` | `2` | `"DepositRateIncreased"` | Tăng tỷ lệ đặt cọc |
| `AuctionSanctionType.TempRestricted` | `3` | `"TempRestricted"` | Hạn chế đấu giá tạm thời |
| `AuctionSanctionType.Suspended` | `4` | `"Suspended"` | Đình chỉ đấu giá |
