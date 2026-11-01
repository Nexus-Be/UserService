# USER SERVICE - POST /api/v1/auth/logout

> **Feature / Use Case:** Auth — Logout
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** AI Assistant (UserService Architecture Team)
> **Mô tả chức năng:** Revokes the supplied refresh token session.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/auth/logout`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous`
- **Quy tắc Sở hữu (Resource Ownership):** Token-based session revocation
- **Controller:** `User.Api/Controllers/Public/AuthController.cs`
- **Application Contract:** `User.Application/Services/Auth/IAuthService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Auth/AuthRequests.cs`
- **Tên C# Record:** `LogoutApiRequest`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed record LogoutApiRequest(
    string? RefreshToken = null,
    string? DeviceInfo = null
);
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "refreshToken": "7c9e6679-7425-40de-944b-e07fc1f90ae7"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Auth/AuthResponses.cs`
- **Tên C# Record:** `LogoutApiResponse`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record LogoutApiResponse(
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
| `400 Bad Request` | `VAL_INVALID_INPUT` | `One or more validation errors occurred.` | Missing token in body and missing Authorization header |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Unhandled exception |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract token from request body or Authorization header.
2. **Bước 2:** Find active session in UserSessions and mark IsRevoked = true.
3. **Bước 3:** Remove cached session keys from Redis if applicable.
4. **Bước 4:** Return LogoutApiResponse with Success = true.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Auth/POST_logout.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Auth/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
