# USER SERVICE - POST /api/v1/auth/google

> **Feature / Use Case:** Auth — Google Login via Firebase
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-21
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Verifies a Firebase ID Token (Google Sign-In), creates or links the User account, and issues internal JWT access token and refresh token.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/auth/google`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous`
- **Quy tắc Sở hữu (Resource Ownership):** N/A (Identified by verified Firebase ID Token)
- **Controller:** `User.Api/Controllers/Public/AuthController.cs`
- **Application Contract:** `User.Application/Services/Auth/IAuthService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Auth/AuthRequests.cs`
- **Tên C# Record:** `GoogleLoginApiRequest`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed record GoogleLoginApiRequest(
    [Required(ErrorMessage = "Firebase ID token is required.")]
    string IdToken,

    string? DeviceInfo = null,
    string? IpAddress = null
);
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "idToken": "eyJhbGciOiJSUzI1NiIsImtpZCI6Ij...",
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
  "email": "buyer@gmail.com",
  "role": "buyer",
  "roles": ["buyer"],
  "permissions": [],
  "user": {
    "id": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
    "email": "buyer@gmail.com",
    "username": null,
    "firstName": "John",
    "lastName": "Doe",
    "roles": ["buyer"],
    "permissions": [],
    "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
    "role": "buyer"
  }
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Firebase ID token is required.` | Empty or whitespace `idToken` |
| `401 Unauthorized` | `INVALID_FIREBASE_TOKEN` | `Invalid or expired Firebase ID token.` | Firebase JWT signature, issuer, audience, or expiration verification failed |
| `400 Bad Request` | `FIREBASE_EMAIL_REQUIRED` | `Google account must provide an email address.` | Verified Firebase token has no email claim |
| `403 Forbidden` | `FIREBASE_EMAIL_NOT_VERIFIED` | `Google email address is not verified.` | `email_verified` claim is false |
| `404 Not Found` | `USER_NOT_FOUND` | `User account does not exist or has been deleted.` | Matched user has `IsDeleted == true` or `Status == UserStatus.Deleted` |
| `403 Forbidden` | `ACCOUNT_BANNED` | `Account has been locked or banned by an administrator. Reason: <banReason>.` | Matched user has `Status == UserStatus.Banned` |
| `409 Conflict` | `FIREBASE_UID_MISMATCH` | `This email is already linked to another Google account.` | Existing user has a different `FirebaseUid` than the token's `uid` |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `UserStatus.PendingVerification` | `0` | `"PendingVerification"` | Activated to `Active` upon verified Google login |
| `UserStatus.Active` | `1` | `"Active"` | Normal active account allowed to login |
| `UserStatus.Banned` | `2` | `"Banned"` | Banned account blocked from login |
| `UserStatus.Deleted` | `3` | `"Deleted"` | Deleted account blocked from login |
| `AuditAction.UserCreate` | `14` | `"UserCreate"` | Recorded when a new user is provisioned via Google Sign-In |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Validate `idToken` is non-empty (`400 VAL_INVALID_INPUT`).
2. **Bước 2:** Verify Firebase ID Token via `IFirebaseAuthService.VerifyIdTokenAsync` (Google JWKS + `JwtSecurityTokenHandler` + `IMemoryCache`); catch verification errors and return `401 INVALID_FIREBASE_TOKEN`.
3. **Bước 3:** Validate `email` is present (`400 FIREBASE_EMAIL_REQUIRED`) and `email_verified == true` (`403 FIREBASE_EMAIL_NOT_VERIFIED`).
4. **Bước 4:** Query `Users` (including `IgnoreQueryFilters()` to detect soft-deleted accounts) by `FirebaseUid == claims.Uid` or `Email == normalizedEmail`.
5. **Bước 5:** Check account status (`Deleted` -> `404 USER_NOT_FOUND`, `Banned` -> `403 ACCOUNT_BANNED`, different `FirebaseUid` -> `409 FIREBASE_UID_MISMATCH`).
6. **Bước 6:** If user does not exist, create a new `User` (`Status = Active`, `PasswordHash = null`, `FirebaseUid = claims.Uid`, `Role = Buyer`, `EmailVerifiedAt = UtcNow`, normalized `FirstName`/`LastName`/`AvatarUrl`), create `ReputationScore.CreateDefault(userId)`, and record `AuditAction.UserCreate` via `_dbContext.AddAuditLog(...)`.
7. **Bước 7:** If user exists, link `FirebaseUid` if empty, activate if `PendingVerification`, and fill `AvatarUrl` if empty.
8. **Bước 8:** Issue internal JWT Access Token and Refresh Token via `IssueAuthTokensAsync` and return `LoginApiResponse`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/A_Active/User/Auth/POST_google.md`.
- [x] **Persistence:** Entity `User` & `UserConfiguration` & Migration `AddFirebaseGoogleLoginSupport`.
- [x] **Application:** `IFirebaseAuthService`, `AuthRequests`, `AuthDtos`, `IAuthService`, `AuthService`.
- [x] **Infrastructure:** `FirebaseAuthService` & `DependencyInjection`.
- [x] **Api:** `AuthController.GoogleLogin` (`POST /api/v1/auth/google`).
- [x] **Tests:** Unit & controller tests in `AuthServiceTests` and `AuthControllerRestTests`.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
