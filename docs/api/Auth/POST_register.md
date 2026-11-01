# USER SERVICE - POST /api/v1/auth/register

> **Feature / Use Case:** Auth — Register
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Vinh & AI Agent
> **Mô tả chức năng:** Registers a new user account and sends an email verification OTP.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/auth/register`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous`
- **Quy tắc Sở hữu (Resource Ownership):** N/A (Public registration)
- **Controller:** `User.Api/Controllers/Public/AuthController.cs`
- **Application Contract:** `User.Application/Services/Auth/IAuthService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Auth/AuthRequests.cs`
- **Tên C# Record:** `RegisterApiRequest`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed record RegisterApiRequest(
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    string Email,

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    string Password,

    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null,
    string? Username = null
);
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "email": "buyer@example.com",
  "password": "SecurePassword123!",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "0987654321",
  "username": "johndoe"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Auth/AuthResponses.cs`
- **Tên C# Record:** `RegisterApiResponse`
- **Mã HTTP thành công:** `201 Created`
- **Khai báo C# Record:**
```csharp
public sealed record RegisterApiResponse(
    string UserId,
    string Email,
    int OtpExpiresIn
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "email": "buyer@example.com",
  "otpExpiresIn": 300
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `One or more validation errors occurred.` | Request validation failed on Email, Password or payload fields |
| `409 Conflict` | `EMAIL_ALREADY_EXISTS` | `Email is already registered in the system.` | Email is already registered by another account |
| `409 Conflict` | `USERNAME_ALREADY_EXISTS` | `Username is already registered in the system.` | Username is already taken by another user |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Unhandled exception or unexpected downstream failure |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `UserStatus.PendingVerification` | `0` | "PendingVerification" | Account awaiting email OTP verification |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Validate request body DataAnnotations at controller boundary.
2. **Bước 2:** Check database for uniqueness of Email and optional Username.
3. **Bước 3:** Hash password securely using BCrypt with salt.
4. **Bước 4:** Persist new User entity with status PendingVerification and default Buyer role.
5. **Bước 5:** Generate 6-digit OTP and cache in Redis with 5-minute TTL (otp:register:{email}).
6. **Bước 6:** Invoke NotificationService via typed REST client IEmailNotificationClient to dispatch verification email.
7. **Bước 7:** Return RegisterApiResponse with HTTP 201 Created.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Auth/POST_register.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Auth/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
