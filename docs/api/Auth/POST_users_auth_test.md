# USER SERVICE - POST /api/v1/users/auth/test

> **Feature / Use Case:** Auth — RegisterTest
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Vinh & AI Agent
> **Mô tả chức năng:** Registers a test user account with instant OTP email dispatch.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/users/auth/test`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous`
- **Quy tắc Sở hữu (Resource Ownership):** N/A (Test registration utility)
- **Controller:** `User.Api/Controllers/Public/AuthController.cs`
- **Application Contract:** `User.Application/Services/Auth/IAuthService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Auth/AuthRequests.cs`
- **Tên C# Record:** `RegisterTestApiRequest`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed record RegisterTestApiRequest(
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    string Email,

    [Required(ErrorMessage = "Password is required.")]
    string Password
);
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "email": "testuser@example.com",
  "password": "Password123!"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Auth/AuthResponses.cs`
- **Tên C# Record:** `RegisterTestApiResponse`
- **Mã HTTP thành công:** `201 Created`
- **Khai báo C# Record:**
```csharp
public sealed record RegisterTestApiResponse(
    bool Success,
    string Message,
    string UserId,
    string Email,
    string Status,
    string Role,
    bool OtpSent,
    int OtpExpiresIn
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "success": true,
  "message": "User registered successfully. Verification email dispatched.",
  "userId": "b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e",
  "email": "testuser@example.com",
  "status": "PendingVerification",
  "role": "Buyer",
  "otpSent": true,
  "otpExpiresIn": 300
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `One or more validation errors occurred.` | Invalid email or password payload |
| `409 Conflict` | `EMAIL_ALREADY_EXISTS` | `Email is already registered in the system.` | Email already exists in database |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Unhandled exception |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `UserStatus.PendingVerification` | `0` | "PendingVerification" | Awaiting OTP |
| `RoleType.Buyer` | `0` | "Buyer" | Standard buyer role |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Validate email and password payload.
2. **Bước 2:** Check if user already exists with given email.
3. **Bước 3:** Create pending buyer user and store hashed password.
4. **Bước 4:** Dispatch OTP via email notification client.
5. **Bước 5:** Return RegisterTestApiResponse with status 201 Created.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Auth/POST_users_auth_test.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Auth/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
