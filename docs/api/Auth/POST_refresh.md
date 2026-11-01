# USER SERVICE - POST /api/v1/auth/refresh

> **Feature / Use Case:** Auth — RefreshToken
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** AI Assistant (UserService Architecture Team)
> **Mô tả chức năng:** Issues a new access token from a valid and unexpired refresh token.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/auth/refresh`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous`
- **Quy tắc Sở hữu (Resource Ownership):** Token-based session validation
- **Controller:** `User.Api/Controllers/Public/AuthController.cs`
- **Application Contract:** `User.Application/Services/Auth/IAuthService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Auth/AuthRequests.cs`
- **Tên C# Record:** `RefreshTokenApiRequest`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed record RefreshTokenApiRequest(
    [Required(ErrorMessage = "RefreshToken is required.")]
    string RefreshToken,

    string? DeviceInfo = null
);
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "refreshToken": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "deviceInfo": "Chrome on Windows"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Auth/AuthResponses.cs`
- **Tên C# Record:** `RefreshTokenApiResponse`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record RefreshTokenApiResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string? UserId = null,
    string? Email = null,
    string? Role = null,
    IReadOnlyList<string>? Roles = null,
    IReadOnlyList<string>? Permissions = null,
    LoginUserApiResponse? User = null
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "8d0f778a-8536-51ef-a55c-f18gd2g01bf8",
  "expiresIn": 900,
  "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "email": "buyer@example.com",
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
| `400 Bad Request` | `VAL_INVALID_INPUT` | `One or more validation errors occurred.` | Empty refresh token string |
| `401 Unauthorized` | `REFRESH_TOKEN_INVALID` | `Refresh token is invalid or has been revoked.` | Token not found or session revoked |
| `401 Unauthorized` | `REFRESH_TOKEN_EXPIRED` | `Refresh token has expired.` | Token past ExpiresAt timestamp |
| `403 Forbidden` | `USER_BANNED` | `User account has been suspended or banned.` | Associated user has been banned |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Locate session record in UserSessions matching refreshToken hash.
2. **Bước 2:** Validate session is active, unrevoked, and not expired.
3. **Bước 3:** Load associated user, roles, and permissions; ensure account is Active.
4. **Bước 4:** Rotate refresh token: revoke existing session and create new session record.
5. **Bước 5:** Generate fresh JWT access token.
6. **Bước 6:** Return RefreshTokenApiResponse with new token pair.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Auth/POST_refresh.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Auth/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
