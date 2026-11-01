# USER SERVICE - PATCH /api/v1/admin/users/{userId}/ban

> **Feature / Use Case:** AdminUsers — BanUser
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Vinh & AI Agent
> **Mô tả chức năng:** Bans a user account for regulatory or policy violations.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PATCH`
- **Route Endpoint:** `/api/v1/admin/users/{userId}/ban`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Admin authority strictly enforced
- **Controller:** `User.Api/Controllers/Public/AdminUsersController.cs`
- **Application Contract:** `User.Application/Services/Admin/IAdminUserService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `userId` | Route | `Guid` | Có | UUIDv4 format | Target user ID |

### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Admin/AdminUserRequests.cs`
- **Tên C# Record:** `BanAdminUserDto`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed class BanAdminUserDto
{
    [Required(ErrorMessage = "Ban reason is required.")]
    public string? Reason { get; set; }
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "reason": "Violating terms of service and fraudulent activity"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Admin/AdminUserResponses.cs`
- **Tên C# Record:** `AdminUserBannedResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record AdminUserBannedResponseDto(
    string UserId,
    string Status,
    string BanReason,
    DateTimeOffset BannedAt,
    string BannedBy,
    int RevokedTokensCount,
    string Message
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "status": "Banned",
  "banReason": "Violating terms of service and fraudulent activity",
  "bannedAt": "2026-11-01T13:30:00Z",
  "bannedBy": "admin-guid-1234",
  "revokedTokensCount": 4,
  "message": "User account has been banned successfully."
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Ban reason is required.` | Empty reason string |
| `400 Bad Request` | `CANNOT_BAN_ADMIN` | `Administrators cannot ban fellow Admin accounts.` | Target user holds Admin role |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Missing bearer token |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Caller is not an Admin |
| `404 Not Found` | `USER_NOT_FOUND` | `User account not found.` | User does not exist in DB |
| `409 Conflict` | `USER_ALREADY_BANNED` | `User account is already banned.` | User status is already Banned |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `UserStatus.Banned` | `2` | "Banned" | Banned account suspended from all actions |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract adminId from token claims.
2. **Bước 2:** Locate target user in database.
3. **Bước 3:** Verify target user is not an Admin (prevent banning peer admins).
4. **Bước 4:** Verify user is not already banned.
5. **Bước 5:** Set Status = UserStatus.Banned, BannedAt = DateTimeOffset.UtcNow, BanReason = dto.Reason.
6. **Bước 6:** Revoke all active sessions in UserSessions for target user.
7. **Bước 7:** Record audit log.
8. **Bước 8:** Return AdminUserBannedResponseDto with 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/AdminUsers/PATCH_admin_users_userId_ban.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/AdminUsers/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
