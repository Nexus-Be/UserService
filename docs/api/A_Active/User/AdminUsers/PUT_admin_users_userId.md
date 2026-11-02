# USER SERVICE - PUT /api/v1/admin/users/{userId}

> **Feature / Use Case:** AdminUsers — UpdateUser
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Updates an existing user account by administrator.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PUT`
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
- **Tên C# Record:** `UpdateAdminUserDto`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed class UpdateAdminUserDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Status { get; set; }

    [Required(ErrorMessage = "Reason is required.")]
    public string? Reason { get; set; }
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "firstName": "Johnathan",
  "lastName": "Doe",
  "phoneNumber": "0987654321",
  "status": "Active",
  "reason": "Administrative correction of user profile details"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Admin/AdminUserResponses.cs`
- **Tên C# Record:** `AdminUserUpdatedResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record AdminUserUpdatedResponseDto(
    string Id,
    string UserId,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string Status,
    string? UpdatedBy,
    DateTimeOffset UpdatedAt,
    string Message
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "id": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "firstName": "Johnathan",
  "lastName": "Doe",
  "phoneNumber": "0987654321",
  "status": "Active",
  "updatedBy": "admin-id-12345",
  "updatedAt": "2026-11-01T13:00:00Z",
  "message": "User updated successfully."
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Reason for updating user is required.` | Missing reason string |
| `400 Bad Request` | `VAL_INVALID_USER_STATUS` | `Invalid user status.` | Status value is invalid |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Missing bearer token |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Missing admin or staff role |
| `404 Not Found` | `USER_NOT_FOUND` | `User was not found in the system.` | Target user does not exist |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `UserStatus.Active` | `1` | "Active" | Active |
| `UserStatus.Banned` | `2` | "Banned" | Banned |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract adminId from token claims.
2. **Bước 2:** Retrieve target user from database.
3. **Bước 3:** Apply modifications (name, phone, status).
4. **Bước 4:** Record audit log in database with old and new values.
5. **Bước 5:** Persist changes in database.
6. **Bước 6:** Return AdminUserUpdatedResponseDto.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/AdminUsers/PUT_admin_users_userId.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/AdminUsers/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
