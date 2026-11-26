# [DELETE] /api/v1/admin/users/{userId}/avatar

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `AdminUser`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Admin gỡ bỏ ảnh đại diện vi phạm của người dùng (kèm lý do và tùy chọn áp dụng chế tài trừ điểm uy tín nếu cần). Ghi nhận Audit Log đầy đủ.
* **HTTP Method:** `DELETE`
* **Đường dẫn chuẩn:** `/api/v1/admin/users/{userId}/avatar`
* **Quyền hạn truy cập (RBAC):** `Admin` (`[RequireRole("admin")]`)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/AdminUsersController.cs` $\rightarrow$ `RemoveUserAvatar()`
* **Application Interface:** `User.Application/Services/Admin/IAdminUserService.cs` $\rightarrow$ `RemoveUserAvatarAsync()`
* **Service Implementation:** `User.Application/Services/Admin/AdminUserService.cs` $\rightarrow$ `RemoveUserAvatarAsync()`
* **Request DTO:** `User.Application/Services/Admin/AdminUserRequests.cs` $\rightarrow$ `RemoveAdminUserAvatarDto` (Optional body)
* **Response DTO:** `User.Application/Services/Admin/AdminUserResponses.cs` $\rightarrow$ `AdminUserAvatarRemovedResponseDto`
* **Thực thể & Bảng DB:** `User.Domain/Entities/User.cs` $\rightarrow$ Bảng `Users` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters
| Tên tham số | Kiểu dữ liệu | Bắt buộc | Ràng buộc & Mô tả |
|:---|:---|:---:|:---|
| `userId` | `Guid` | Có | Định danh tài khoản người dùng cần gỡ avatar. |

### 3.2. Request Body (`RemoveAdminUserAvatarDto` - Không bắt buộc)
```json
{
  "reason": "Inappropriate profile picture violating community guidelines.",
  "applyPenalty": true
}
```

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "userId": "dddddddd-4444-4444-4444-444444444444",
  "removedBy": "aaaaaaaa-1111-1111-1111-111111111111",
  "removedAt": "2026-11-12T10:15:00Z",
  "reason": "Inappropriate profile picture violating community guidelines.",
  "penaltyApplied": true,
  "message": "User avatar removed successfully."
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Thiếu hoặc token không hợp lệ. |
| `403` | `FORBIDDEN` | "You do not have permission to access this resource." | Người dùng không có vai trò `admin`. |
| `404` | `USER_NOT_FOUND` | "User account not found." | Không tìm thấy tài khoản người dùng theo `userId`. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IAdminUserService.cs` và `AdminUserService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `AdminUsersController.cs` (`DELETE /api/v1/admin/users/{userId}/avatar`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test User.sln -c Release` vượt qua 100%.
