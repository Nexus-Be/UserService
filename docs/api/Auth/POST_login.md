# USER SERVICE - POST /api/v1/auth/login

> **Feature / Use Case:** Auth — Login
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Vinh & AI Agent
> **Mô tả chức năng:** Authenticates a user and issues JWT access token and refresh token.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/auth/login`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous`
- **Quy tắc Sở hữu (Resource Ownership):** N/A (Identified by credentials)
- **Controller:** `User.Api/Controllers/Public/AuthController.cs`
- **Application Contract:** `User.Application/Services/Auth/IAuthService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Auth/AuthRequests.cs`
- **Tên C# Record:** `LoginApiRequest`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed record LoginApiRequest(
    [Required(ErrorMessage = "Identifier is required.")]
    string Identifier,

    [Required(ErrorMessage = "Password is required.")]
    string Password,

    string? DeviceInfo = null,
    string? IpAddress = null
);
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "identifier": "buyer@example.com",
  "password": "SecurePassword123!",
  "deviceInfo": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
  "ipAddress": "192.168.1.100"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Auth/AuthResponses.cs`
- **Tên C# Record:** `LoginApiResponse`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record LoginApiResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    LoginUserApiResponse User,
    string? UserId = null,
    string? Email = null,
    string? Role = null,
    IReadOnlyList<string>? Roles = null,
    IReadOnlyList<string>? Permissions = null
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "email": "buyer@example.com",
  "role": "Buyer",
  "roles": ["Buyer"],
  "permissions": ["view_products", "create_order"],
  "user": {
    "id": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
    "email": "buyer@example.com",
    "username": "buyer01",
    "firstName": "John",
    "lastName": "Doe",
    "roles": ["Buyer"],
    "permissions": ["view_products", "create_order"]
  }
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `One or more validation errors occurred.` | Empty identifier or password |
| `401 Unauthorized` | `INVALID_CREDENTIALS` | `Invalid username, email, or password.` | User not found or BCrypt password verification failed |
| `403 Forbidden` | `ACCOUNT_BANNED` | `Account has been locked or banned by an administrator. Reason: <banReason>.` | User account is banned (includes `Reason: <banReason>` if specified by admin) |
| `403 Forbidden` | `ACCOUNT_NOT_VERIFIED` | `Account has not been verified with OTP.` | User status is PendingVerification |
| `423 Locked` | `ACCOUNT_LOCKED_TEMPORARY` | `Account is temporarily locked for 30 minutes due to 5 consecutive failed login attempts.` | User reached 5 consecutive failed login attempts |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `UserStatus.Active` | `1` | "Active" | Normal active account allowed to login |
| `UserStatus.Banned` | `2` | "Banned" | Banned account blocked from login |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Lookup user by email or username, including roles and permissions.
2. **Bước 2:** Verify BCrypt password hash against stored PasswordHash.
3. **Bước 3:** Check user account status (block if Banned or PendingVerification).
4. **Bước 4:** Generate JWT access token with claims (sub, email, roles, permissions).
5. **Bước 5:** Generate opaque refresh token and persist UserSession in database with expiry.
6. **Bước 6:** Return LoginApiResponse with tokens and user details.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Auth/POST_login.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Auth/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
