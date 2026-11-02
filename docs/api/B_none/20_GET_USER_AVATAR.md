# [GET] /api/v1/users/{userId}/avatar

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Users`  
> **Mã tham chiếu Excel:** `20`  
> **Trạng thái tài liệu:** `B_none (Chờ hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `⏳ IN_PROGRESS`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Xem ảnh avatar của từng user
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/users/{userId}/avatar`
* **Đường dẫn gốc trong Excel:** `/api/v1/user/{userId}/avatar` (Chuẩn hóa từ '/api/v1/user/{userId}/avatar' sang chuẩn 100% số nhiều '/api/v1/users/{userId}/avatar'.)
* **Quyền hạn truy cập (RBAC):** `Buyer`
* **Yêu cầu Idempotency:** Không áp dụng (Idempotent theo HTTP spec)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

Mọi thay đổi trong hàm nghiệp vụ (thêm validation, ném exception, thêm enum) bắt buộc phải cập nhật đồng bộ vào tài liệu này:

* **Microservice thụ lý:** `src/Services/User/`
* **Cổng Gateway điều phối:** `src/Services/Gateway/Gateway/appsettings.json` (Route: `users-plural-route`)
* **Controller Action:** `User.Api/Controllers/UsersController.cs` $\rightarrow$ `GetUserAvatar()`
* **Application Interface:** `User.Application/Services/Users/IUserService.cs` $\rightarrow$ `GetUserAvatarAsync()`
* **Service Implementation:** `User.Application/Services/Users/UserService.cs` $\rightarrow$ `GetUserAvatarAsync()`
* **Thực thể & Bảng DB:** `User.Domain/Entities/User.cs` $\rightarrow$ Bảng `Users` trong PostgreSQL
* **Typed HTTP Client (nếu có):** N/A (Giao tiếp nội bộ trực tiếp qua DbContext)

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters (Tham số đường dẫn)
| Tên tham số | Kiểu dữ liệu | Bắt buộc? | Ràng buộc & Định dạng |
|:---|:---|:---:|:---|
| `userId` | `Guid (UUID)` | Có | Mã định danh duy nhất (UUIDv4) |

### 3.2. Query Parameters (Tham số truy vấn)
*Không có tham số truy vấn.*

### 3.3. Request Body (JSON Payload)
```json
Path:
{
"userId": "uuid"
}
```


---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE ENVELOPE)

### 4.1. Phản hồi Thành công Đơn lẻ (`ApiResponse<T>`)
Áp dụng cho API trả về 1 đối tượng hoặc kết quả thực thi:
```json
200 OK:
{
  "success": true,
  "statusCode": 200,
  "message": "Avatar retrieved successfully.",
  "data": {
    "userId": "uuid",
    "avatarUrl": "string"
  },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "timestamp": "2026-10-26T00:00:00Z"
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

Frontend bắt buộc dựa vào cột `errorCode` để map và hiển thị giao diện phù hợp. Khi backend function bổ sung lệnh ném lỗi, phải cập nhật ngay vào bảng này:

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `400` | `VAL_INVALID_INPUT` | "One or more validation errors occurred." | Dữ liệu đầu vào vi phạm FluentValidation hoặc model binding. |
| `401` | `UNAUTHORIZED` | "Authentication token is missing, expired, or invalid." | Người dùng chưa đăng nhập hoặc JWT không hợp lệ. |
| `404` | `USER_NOT_FOUND` | "User was not found." | Điều kiện nghiệp vụ đặc thù: USER_NOT_FOUND. |
| `403` | `FORBIDDEN` | "You do not have permission to perform this action." | Người dùng không đủ quyền RBAC theo quy định. |
| `404` | `USER_NOT_FOUND` | "User not found with the specified ID." | Không tìm thấy bản ghi trong Database tương ứng. |
| `500` | `INTERNAL_SERVER_ERROR` | "An unexpected error occurred. Please try again later." | Lỗi hệ thống nội bộ chưa được xử lý. |

---

## 6. DANH MỤC ENUM LIÊN QUAN (DOMAIN ENUMS)

Bảng đối chiếu toàn diện giữa trường dữ liệu trong Payload (JSON Field), định dạng lưu trữ Database và chuỗi trả về API để Frontend hiển thị và binding giao diện chính xác:

| Tên trường Payload (JSON Field) | Kiểu C# Enum | Giá trị DB (`integer`) | Giá trị API (`string PascalCase`) | Ý nghĩa nghiệp vụ & Hướng dẫn hiển thị Frontend |
|:---|:---|:---:|:---|:---|
| `status` | `UserStatus` | `0` | `PendingVerification` | **Chờ xác thực OTP**: Tài khoản mới đăng ký, chưa kích hoạt email/SĐT. Badge vàng (Warning). |
| `status` | `UserStatus` | `1` | `Active` | **Đang hoạt động**: Tài khoản đã kích hoạt, đầy đủ quyền mua/bán theo role. Badge xanh lá (Success). |
| `status` | `UserStatus` | `2` | `Banned` | **Bị cấm / Khóa**: Tài khoản vi phạm quy định, bị Admin khóa. Badge đỏ (Danger). |
| `status` | `UserStatus` | `3` | `Deleted` | **Đã xóa mềm**: Tài khoản đã hủy hoặc xóa khỏi hệ thống. Badge xám (Muted). |
| `gender` | `Gender` | `0` | `Unspecified` | **Chưa xác định / Không tiết lộ**: Người dùng chưa chọn giới tính. |
| `gender` | `Gender` | `1` | `Male` | **Nam**: Giới tính nam. |
| `gender` | `Gender` | `2` | `Female` | **Nữ**: Giới tính nữ. |
| `gender` | `Gender` | `3` | `Other` | **Khác**: Giới tính khác. |

---

## 7. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [ ] **1. Tầng Application:** `IUserService.cs` và `UserService.cs` đã được triển khai hoàn chỉnh.
- [ ] **2. Tầng Api:** `UsersController.cs` ghép cặp 1-to-1, route số nhiều, summary XML ngắn gọn.
- [ ] **3. Bắt lỗi & Validation:** Toàn bộ mã lỗi trong ma trận ở Mục 5 đã được kiểm soát trong code.
- [ ] **4. Enum Mapping:** Toàn bộ enum ở Mục 6 được lưu integer trong DB và serialize PascalCase ở API.
- [ ] **5. Kiểm thử tự động:** `dotnet test` vượt qua 100% các bài test (Kèm tên file test: `UsersTests.cs`).
- [ ] **6. Định tuyến Gateway:** Route YARP `users-plural-route` đã được cấu hình trong `appsettings.json` và test thông suốt.
- [ ] **7. Xác nhận của Sếp (Human Approval):** Đã kiểm thử thực tế và cho phép chuyển sang `docs/api/A_Active/`.

> **Trạng thái nghiệm thu:** `[ ⏳ IN_PROGRESS (Lưu tại B_none) ]`
