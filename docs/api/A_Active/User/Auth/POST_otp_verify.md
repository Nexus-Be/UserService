# USER SERVICE - POST /api/v1/auth/otp/verify

> **Feature / Use Case:** Auth — VerifyOtp
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Verifies the OTP sent to newly registered account to activate user.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/auth/otp/verify`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous`
- **Quy tắc Sở hữu (Resource Ownership):** N/A (Identified by email and OTP token)
- **Controller:** `User.Api/Controllers/Public/AuthController.cs`
- **Application Contract:** `User.Application/Services/Auth/IAuthService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Auth/AuthRequests.cs`
- **Tên C# Record:** `VerifyOtpApiRequest`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed record VerifyOtpApiRequest(
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    string Email,

    [Required(ErrorMessage = "OTP code is required.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be exactly 6 characters.")]
    string Otp
);
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "email": "buyer@example.com",
  "otp": "123456"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Auth/AuthResponses.cs`
- **Tên C# Record:** `VerifyOtpApiResponse`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record VerifyOtpApiResponse(
    bool IsVerified,
    string Email,
    string? UserId = null,
    string? Role = null,
    IReadOnlyList<string>? Roles = null,
    IReadOnlyList<string>? Permissions = null
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "isVerified": true,
  "email": "buyer@example.com",
  "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "role": "Buyer",
  "roles": ["Buyer"],
  "permissions": ["view_products", "create_order"]
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `One or more validation errors occurred.` | Empty email or invalid OTP format |
| `400 Bad Request` | `OTP_INVALID` | `Invalid OTP code.` | Provided OTP code does not match Redis value |
| `400 Bad Request` | `OTP_EXPIRED` | `OTP has expired.` | OTP key in Redis has expired |
| `404 Not Found` | `USER_NOT_FOUND` | `User account not found with the specified email.` | No user exists for the given email |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `UserStatus.Active` | `1` | "Active" | Account activated upon OTP verification |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Validate request body parameters.
2. **Bước 2:** Retrieve user by email; throw USER_NOT_FOUND if absent.
3. **Bước 3:** Check OTP against cached value in Redis.
4. **Bước 4:** Transition User.Status from PendingVerification to Active; set EmailVerifiedAt.
5. **Bước 5:** Remove consumed OTP from Redis.
6. **Bước 6:** Dispatched non-blocking initial wallet creation via `IWalletServiceClient` (`POST api/v1/wallets`).
7. **Bước 7:** Record audit log for account activation.
8. **Bước 8:** Return VerifyOtpApiResponse with roles and permissions.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Auth/POST_otp_verify.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Auth/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
