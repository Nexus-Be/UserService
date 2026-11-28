# USER SERVICE - GET /api/v1/admin/users/{userId}/auction-violations

> **Feature / Use Case:** AdminAuctionViolations (#21)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-14
> **Tác giả:** Hiển Vinh & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/admin/users/{userId}/auction-violations`
- **Quyền hạn (RBAC Policy):** `Admin` hoặc `Staff` (`[RequireRole("admin", "staff")]`)
- **Controller:** `User.Api/Controllers/Public/AdminAuctionViolationsController.cs` (`GetAuctionViolations`)
- **Application Contract:** `User.Application/Services/AuctionViolations/IAuctionViolationAdminService.cs` (`GetAuctionViolationsAsync`)

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `userId` | Route | `Guid` | Có | UUID format | Định danh Buyer cần tra cứu |
| `page` | Query | `int` | Không | `>= 1`, Default `1` | Số trang |
| `pageSize` | Query | `int` | Không | `1 - 100`, Default `20` | Số bản ghi mỗi trang |

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `User.Application/Services/AuctionViolations/AuctionViolationAdminResponses.cs`
- **Tên C# Record:** `AdminAuctionViolationsResponse` (sử dụng `PagedData<AdminAuctionViolationItemDto>`)
- **Mã HTTP thành công:** `200 OK`
- **JSON Response Body mẫu (camelCase):**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "currentStreak": 2,
  "auctionRestriction": {
    "isRestricted": false,
    "type": null,
    "until": null,
    "reason": null
  },
  "violations": {
    "items": [
      {
        "violationId": "11111111-2222-3333-4444-555555555555",
        "violationType": "WinnerNonPayment",
        "auctionId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
        "orderId": "8f14e45f-ceea-467a-9c56-9a3e2c112233",
        "streakAfter": 2,
        "reputationDelta": -10,
        "depositForfeitedAmount": 50000,
        "sanction": "DepositRateIncreased",
        "sanctionUntil": null,
        "reason": null,
        "createdAt": "2026-11-13T10:00:01Z",
        "source": "System"
      }
    ],
    "pagination": {
      "page": 1,
      "pageSize": 20,
      "totalCount": 1,
      "totalPages": 1
    }
  }
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Page must be greater than or equal to 1.` / `Page size must be between 1 and 100.` | Tham số phân trang không hợp lệ |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Thiếu hoặc token không hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Không có role `admin` hoặc `staff` |
| `404 Not Found` | `USER_NOT_FOUND` | `User account was not found.` | Không tìm thấy người dùng |
