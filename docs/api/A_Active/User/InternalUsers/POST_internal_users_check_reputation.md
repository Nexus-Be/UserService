# USER SERVICE - POST /api/v1/internal/users/check-reputation

> **Feature / Use Case:** InternalUsers — CheckReputation
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-05
> **Tác giả:** Hiển Vinh & AI Agent
> **Caller:** AuctionService
> **Mô tả chức năng:** Kiểm tra tư cách và uy tín của người bán (Seller) khi tạo phiên đấu giá. Yêu cầu tài khoản đang Active, SellerProfile đã được phê duyệt (Approved) và ReputationScore đạt ngưỡng tối thiểu (>= 50).

---

## 1. Thông tin Định tuyến & Xác thực (Routing & Auth)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/internal/users/check-reputation`
- **Bảo mật:** Header `X-Internal-Service-Key`
- **Controller:** `User.Api/Controllers/Internal/InternalUsersController.cs`
- **Application Contract:** `User.Application/Services/Internal/IInternalUserService.cs`

---

## 2. Đầu vào & Validation (Input Schema)
### Request Headers
| Header | Kiểu | Bắt buộc | Ghi chú |
| :--- | :--- | :---: | :--- |
| `X-Internal-Service-Key` | `string` | Có | Khóa định danh liên service nội bộ |

### Request Body (`CheckReputationRequest`)
```json
{
  "action": "SELL",
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
  "isApprovedSeller": true,
  "userStatus": "Active",
  "failureReason": null
}
```

### 3.2 Bị từ chối (`200 OK`)
> Luôn có `isApprovedSeller: false` khi bị từ chối.
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "allowed": false,
  "isApprovedSeller": false,
  "userStatus": "Active",
  "failureReason": "ERR_ACCOUNT_UNVERIFIED"
}
```

### 3.3 Danh sách mã lỗi `failureReason`
- `ERR_USER_NOT_FOUND`: Người dùng không tồn tại hoặc đã bị xóa.
- `ERR_ACCOUNT_BANNED`: Tài khoản bị khóa vĩnh viễn.
- `ERR_ACCOUNT_LOCKED_TEMPORARY`: Tài khoản có án phạt tạm thời còn hiệu lực.
- `ERR_ACCOUNT_UNVERIFIED`: Chưa xác thực tài khoản hoặc chưa được duyệt SellerProfile.
- `ERR_REPUTATION_TOO_LOW`: Điểm uy tín dưới ngưỡng 50.
