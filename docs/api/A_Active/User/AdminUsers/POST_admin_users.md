# USER SERVICE - POST /api/v1/admin/users

> **Feature / Use Case:** AdminUsers — CreateUser
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Cập nhật:** 2026-11-05
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Quản trị viên (Admin) hoặc Nhân viên hỗ trợ (Support Staff) khởi tạo tài khoản người dùng trực tiếp trên hệ thống với 1 vai trò chính duy nhất (Admin, SupportStaff hoặc Buyer).

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/admin/users`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin", "staff")`
- **Quy tắc Sở hữu (Resource Ownership):** Admin và Staff có thẩm quyền tạo tài khoản quản trị nội bộ hoặc hỗ trợ khách hàng.
- **Controller:** `User.Api/Controllers/Public/AdminUsersController.cs`
- **Application Contract:** `User.Application/Services/Admin/IAdminUserService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
*Endpoint này không có tham số đường dẫn.*

### 2.2 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Admin/AdminUserRequests.cs`
- **Tên C# Record:** `CreateAdminUserDto`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed class CreateAdminUserDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Valid email address is required.")]
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [Required(ErrorMessage = "First name is required.")]
    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [Required(ErrorMessage = "Last name is required.")]
    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("roleCodes")]
    public List<string>? RoleCodes { get; set; }
}
```
- **JSON Request Body mẫu (camelCase - 1 User 1 Role):**
```json
{
  "email": "staff.member@company.com",
  "password": "StaffPassword123!",
  "firstName": "Alice",
  "lastName": "Smith",
  "phoneNumber": "0911223344",
  "username": "alicesmith",
  "roleCodes": [
    "SupportStaff"
  ]
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

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
  "roles": [
    "SupportStaff"
  ],
  "status": "Active",
  "createdAt": "2026-11-05T12:00:00Z"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `User creation payload is required.` | Payload rỗng hoặc vi phạm DataAnnotations |
| `400 Bad Request` | `SOD_VIOLATION` | `A user cannot be assigned both Admin and SupportStaff roles simultaneously.` | Cố tình gán đồng thời vai trò Admin và SupportStaff |
| `400 Bad Request` | `MULTIPLE_ROLES_NOT_ALLOWED` | `A user can only be assigned exactly one primary role.` | Truyền nhiều hơn 1 vai trò trong `roleCodes` |
| `400 Bad Request` | `CANNOT_ASSIGN_SELLER_ROLE_DIRECTLY` | `Seller role cannot be assigned directly. Use seller registration workflow.` | Cố tình gán vai trò Seller khi tạo tài khoản trực tiếp |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Thiếu hoặc token không hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Người gọi không có vai trò Admin hoặc Staff |
| `409 Conflict` | `EMAIL_ALREADY_EXISTS` | `Email is already registered in the system.` | Email đăng ký đã tồn tại trong hệ thống |
| `409 Conflict` | `USERNAME_ALREADY_EXISTS` | `Username is already registered in the system.` | Tên đăng nhập đã tồn tại trong hệ thống |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống cơ sở dữ liệu không mong muốn |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `UserStatus.Active` | `1` | `"Active"` | Tài khoản do Admin khởi tạo được kích hoạt ngay lập tức |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Trích xuất `adminId` từ JWT qua `HttpContext.GetUserId()` và kiểm tra vai trò `admin`/`staff`.
2. **Bước 2:** Validate tính hợp lệ của request payload qua DataAnnotations.
3. **Bước 3:** Kiểm tra tính duy nhất của email và username trong bảng `Users`.
4. **Bước 4:** Kiểm tra quy tắc phân tách nhiệm vụ (SoD) và quy tắc 1 User 1 Role (`MULTIPLE_ROLES_NOT_ALLOWED`).
5. **Bước 5:** Băm mật khẩu bằng BCrypt có salt (`BCrypt.Net-Next`).
6. **Bước 6:** Gán vai trò hợp lệ, lưu vào cơ sở dữ liệu và ghi `AuditLog` qua `_dbContext.AddAuditLog(...)`.
7. **Bước 7:** Trả về `AdminUserCreatedResponseDto` kèm mã HTTP 201 Created.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/A_Active/User/AdminUsers/POST_admin_users.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Logic kiểm soát SoD và 1 role đã tích hợp trong `AdminUserService.CreateUserAsync`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
