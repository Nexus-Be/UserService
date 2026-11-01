# USER SERVICE - DELETE /api/v1/admin/users/{userId}

> **Feature / Use Case:** AdminUsers — DeleteUser
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** AI Assistant (UserService Architecture Team)
> **Mô tả chức năng:** Soft-deletes a user account and revokes active sessions.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `DELETE`
- **Route Endpoint:** `/api/v1/admin/users/{userId}`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin", "staff")`
- **Quy tắc Sở hữu (Resource Ownership):** Admin/Staff authority
- **Controller:** `User.Api/Controllers/Public/AdminUsersController.cs`
- **Application Contract:** `User.Application/Services/Admin/IAdminUserService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `userId` | Route | `Guid` | Có | UUIDv4 format | Target user ID |

### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Admin/AdminUserResponses.cs`
- **Tên C# Record:** `DeleteAdminUserDto`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed class DeleteAdminUserDto
{
    public string? Reason { get; set; }
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "reason": "Account closure requested by user"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `None (HTTP 204 NoContent)`
- **Tên C# Record:** `None`
- **Mã HTTP thành công:** `204 NoContent`
- **Khai báo C# Record:**
```csharp
// Returns HTTP 204 NoContent with empty body
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json

```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Target user ID is required.` | Malformed user GUID |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Missing bearer token |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Missing admin or staff role |
| `404 Not Found` | `USER_NOT_FOUND` | `User was not found in the system.` | Target user does not exist |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `UserStatus.Deleted` | `3` | "Deleted" | Soft-deleted account |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract adminId from token claims.
2. **Bước 2:** Query user in database; verify not already deleted.
3. **Bước 3:** Set user.Status = UserStatus.Deleted, IsDeleted = true, DeletedAt = DateTimeOffset.UtcNow.
4. **Bước 4:** Revoke all active sessions of this user in UserSessions.
5. **Bước 5:** Record audit log.
6. **Bước 6:** Return 204 NoContent.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/AdminUsers/DELETE_admin_users_userId.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/AdminUsers/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
