# USER SERVICE - POST /api/v1/admin/users

> **Feature / Use Case:** AdminUsers — CreateUser
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** AI Assistant (UserService Architecture Team)
> **Mô tả chức năng:** Creates a user account on behalf of an administrator.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/admin/users`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin", "staff")`
- **Quy tắc Sở hữu (Resource Ownership):** Admin/Staff authority
- **Controller:** `User.Api/Controllers/Public/AdminUsersController.cs`
- **Application Contract:** `User.Application/Services/Admin/IAdminUserService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Admin/AdminUserRequests.cs`
- **Tên C# Record:** `CreateAdminUserDto`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed class CreateAdminUserDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Valid email address is required.")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    public string? Password { get; set; }

    [Required(ErrorMessage = "First name is required.")]
    public string? FirstName { get; set; }

    [Required(ErrorMessage = "Last name is required.")]
    public string? LastName { get; set; }

    public string? PhoneNumber { get; set; }
    public string? Username { get; set; }
    public List<string>? RoleCodes { get; set; }
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "email": "staff.member@company.com",
  "password": "StaffPassword123!",
  "firstName": "Alice",
  "lastName": "Smith",
  "phoneNumber": "0911223344",
  "username": "alicesmith",
  "roleCodes": ["SupportStaff"]
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Admin/AdminUserResponses.cs`
- **Tên C# Record:** `AdminUserCreatedResponseDto`
- **Mã HTTP thành công:** `201 Created`
- **Khai báo C# Record:**
```csharp
public sealed record AdminUserCreatedResponseDto(
    string UserId,
    string Email,
    List<string> Roles,
    string Status,
    DateTimeOffset CreatedAt
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "userId": "e5f6a7b8-c9d0-1e2f-3a4b-5c6d7e8f9a0b",
  "email": "staff.member@company.com",
  "roles": ["SupportStaff"],
  "status": "Active",
  "createdAt": "2026-11-01T12:00:00Z"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `User creation payload is required.` | Empty payload or validation failure |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Missing or expired token |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Missing admin/staff role |
| `409 Conflict` | `EMAIL_ALREADY_EXISTS` | `Email is already registered in the system.` | Target email already in use |
| `409 Conflict` | `USERNAME_ALREADY_EXISTS` | `Username is already registered in the system.` | Target username already in use |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `UserStatus.Active` | `1` | "Active" | Admin-created accounts are immediately activated |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract adminId from token claims.
2. **Bước 2:** Validate input payload DataAnnotations.
3. **Bước 3:** Check email and username uniqueness.
4. **Bước 4:** Hash password with BCrypt.
5. **Bước 5:** Assign specified roles (or default Buyer role).
6. **Bước 6:** Persist User and record AuditLog via _dbContext.AddAuditLog(...).
7. **Bước 7:** Return 201 Created with AdminUserCreatedResponseDto.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/AdminUsers/POST_admin_users.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/AdminUsers/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
