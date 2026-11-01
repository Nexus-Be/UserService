# USER SERVICE - PATCH /api/v1/auth/password

> **Feature / Use Case:** Auth — ChangePassword
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Vinh & AI Agent
> **Mô tả chức năng:** Changes the password of the currently authenticated user.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PATCH`
- **Route Endpoint:** `/api/v1/auth/password`
- **Quyền hạn (RBAC Policy):** `RequireRole (Authenticated)`
- **Quy tắc Sở hữu (Resource Ownership):** currentUserId == target user
- **Controller:** `User.Api/Controllers/Public/AuthController.cs`
- **Application Contract:** `User.Application/Services/Auth/IAuthService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Auth/AuthRequests.cs`
- **Tên C# Record:** `ChangePasswordApiRequest`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed record ChangePasswordApiRequest(
    [Required(ErrorMessage = "Old password is required.")]
    string OldPassword,

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    string NewPassword,

    [Required(ErrorMessage = "Confirm password is required.")]
    string ConfirmPassword
);
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "oldPassword": "CurrentPassword123!",
  "newPassword": "BrandNewPassword123!",
  "confirmPassword": "BrandNewPassword123!"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Auth/AuthResponses.cs`
- **Tên C# Record:** `ChangePasswordApiResponse`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record ChangePasswordApiResponse(
    bool Success
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "success": true
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `One or more validation errors occurred.` | Empty fields or length validation failed |
| `400 Bad Request` | `PASSWORDS_DO_NOT_MATCH` | `New password and confirm password do not match.` | NewPassword != ConfirmPassword |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Invalid or missing Bearer token |
| `401 Unauthorized` | `INVALID_CREDENTIALS` | `Current password is incorrect.` | BCrypt verification of OldPassword failed |
| `404 Not Found` | `USER_NOT_FOUND` | `User account not found.` | Target user does not exist |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract currentUserId from token claims.
2. **Bước 2:** Retrieve user from database.
3. **Bước 3:** Verify OldPassword against current PasswordHash.
4. **Bước 4:** Validate NewPassword != OldPassword and matches ConfirmPassword.
5. **Bước 5:** Update PasswordHash with BCrypt hash of NewPassword.
6. **Bước 6:** Record password change in AuditLogs.
7. **Bước 7:** Return ChangePasswordApiResponse with Success = true.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Auth/PATCH_password.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Auth/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
