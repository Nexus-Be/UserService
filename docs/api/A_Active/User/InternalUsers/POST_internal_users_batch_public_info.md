# USER SERVICE - POST /api/v1/internal/users/batch-public-info

> **Feature / Use Case:** InternalUsers — BatchPublicInfo
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-05
> **Tác giả:** Hiển Vinh & AI Agent
> **Caller:** AuctionService
> **Mô tả chức năng:** Tra cứu danh sách thông tin công khai và tên che (`maskedName`) phục vụ bảng lịch sử đấu giá (tối đa 100 User). Bỏ qua User không tồn tại. Bao gồm cả User đã xóa mềm (`isDeleted: true`) với tên che tương ứng.

---

## 1. Thông tin Định tuyến & Xác thực (Routing & Auth)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/internal/users/batch-public-info`
- **Bảo mật:** Header `X-Internal-Service-Key`
- **Controller:** `User.Api/Controllers/Internal/InternalUsersController.cs`
- **Application Contract:** `User.Application/Services/Internal/IInternalUserService.cs`

---

## 2. Đầu vào & Validation (Input Schema)
### Request Headers
| Header | Kiểu | Bắt buộc | Ghi chú |
| :--- | :--- | :---: | :--- |
| `X-Internal-Service-Key` | `string` | Có | Khóa định danh liên service nội bộ |

### Request Body (`BatchPublicInfoRequest`)
```json
{
  "userIds": [
    "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "8ba34f64-1234-4562-b3fc-9c963f66bbb2"
  ]
}
```
*Ràng buộc:* Mảng `userIds` tối thiểu 1 phần tử, tối đa 100 phần tử. Tự động deduplicate các ID trùng lặp.

---

## 3. Đầu ra (Response Contract - Pure DTO Array)
> UserService CHỈ trả về mảng DTO thuần.

### 3.1 Thành công (`200 OK`)
```json
[
  {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "maskedName": "N***n",
    "isDeleted": false
  },
  {
    "userId": "8ba34f64-1234-4562-b3fc-9c963f66bbb2",
    "maskedName": "d***r",
    "isDeleted": true
  }
]
```
- User không có trong cơ sở dữ liệu sẽ bị loại khỏi mảng kết quả.
- Thuật toán MaskName: Ưu tiên `Username`, nếu không lấy `${LastName} ${FirstName}`. Giữ ký tự đầu và ký tự cuối, ở giữa là `***` (chuỗi $\le 2$ ký tự: ký tự đầu + `*`).
