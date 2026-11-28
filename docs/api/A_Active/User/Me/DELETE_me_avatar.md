# [DELETE] /api/v1/me/avatar

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Profile`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Người dùng tự xóa ảnh đại diện cá nhân hiện tại của mình (gỡ URL avatar khỏi hồ sơ và dọn tài nguyên nếu có).
* **HTTP Method:** `DELETE`
* **Đường dẫn chuẩn:** `/api/v1/me/avatar`
* **Quyền hạn truy cập (RBAC):** `Authenticated` (`[RequireRole]`)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/MeController.cs` $\rightarrow$ `DeleteMyAvatar()`
* **Application Interface:** `User.Application/Services/Profile/IProfileService.cs` $\rightarrow$ `DeleteMyAvatarAsync()`
* **Service Implementation:** `User.Application/Services/Profile/ProfileService.cs` $\rightarrow$ `DeleteMyAvatarAsync()`
* **Thực thể & Bảng DB:** `User.Domain/Entities/User.cs` $\rightarrow$ Bảng `Users` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

* Không có tham số Route, Query hay Request Body. Định danh người dùng được trích xuất từ `HttpContext.GetUserId()`.

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

* **Mã HTTP thành công:** `204 No Content` (Không trả về body).

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Thiếu hoặc token không hợp lệ. |
| `404` | `USER_NOT_FOUND` | "User account not found." | Không tìm thấy tài khoản người dùng hiện tại trong DB. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IProfileService.cs` và `ProfileService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `MeController.cs` (`DELETE /api/v1/me/avatar`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test User.sln -c Release` vượt qua 100%.
