# USER SERVICE - POST /api/v1/internal/users/verify-bid-eligibility

> **Feature / Use Case:** InternalUsers — VerifyBidEligibility
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-05
> **Tác giả:** Hiển Vinh & AI Agent
> **Caller:** AuctionService
> **SLA:** Độ trễ phản hồi < 800ms
> **Mô tả chức năng:** Xác thực tư cách người mua (Buyer) khi đặt giá trong phiên đấu giá. Kiểm tra tài khoản Active, không dính án phạt tạm thời/banned, và ReputationScore >= ngưỡng cấu hình hệ thống (`AUCTION_MIN_REPUTATION_BID`, mặc định 50). Cấu hình được lưu bộ nhớ đệm `IMemoryCache` (TTL 1 phút, key `user:config:AUCTION_MIN_REPUTATION_BID`) để đảm bảo nghiêm ngặt SLA dưới 800ms mà không tốn truy vấn DB lặp lại.

---

## 1. Thông tin Định tuyến & Xác thực (Routing & Auth)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/internal/users/verify-bid-eligibility`
- **Bảo mật:** Header `X-Internal-Service-Key`
- **Controller:** `User.Api/Controllers/Internal/InternalUsersController.cs`
- **Application Contract:** `User.Application/Services/Internal/IInternalUserService.cs`

---

## 2. Đầu vào & Validation (Input Schema)
### Request Headers
| Header | Kiểu | Bắt buộc | Ghi chú |
| :--- | :--- | :---: | :--- |
| `X-Internal-Service-Key` | `string` | Có | Khóa định danh liên service nội bộ |

### Request Body (`VerifyBidEligibilityRequest`)
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

---

## 3. Đầu ra (Response Contract - Pure DTO)
> UserService CHỈ trả về DTO thuần, KHÔNG bọc qua envelope `{ success: true }`.

### 3.1 Thành công / Hợp lệ (`200 OK`)
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "allowed": true,
  "failureReason": null
}
```

### 3.2 Bị từ chối (`200 OK`)
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "allowed": false,
  "failureReason": "ERR_REPUTATION_TOO_LOW"
}
```

### 3.3 Danh sách mã lỗi `failureReason`
- `ERR_USER_NOT_FOUND`: Người dùng không tồn tại hoặc đã bị xóa.
- `ERR_ACCOUNT_BANNED`: Tài khoản bị khóa vĩnh viễn.
- `ERR_ACCOUNT_LOCKED_TEMPORARY`: Tài khoản có án phạt tạm thời còn hiệu lực.
- `ERR_ACCOUNT_UNVERIFIED`: Chưa xác thực tài khoản.
- `ERR_REPUTATION_TOO_LOW`: Điểm uy tín dưới ngưỡng cấu hình hệ thống (`AUCTION_MIN_REPUTATION_BID`).
