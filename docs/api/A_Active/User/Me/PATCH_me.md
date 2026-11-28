# USER SERVICE - PATCH /api/v1/me

> **Feature / Use Case:** Me — UpdateCurrentUserProfile
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Updates editable profile fields of the authenticated user.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PATCH`
- **Route Endpoint:** `/api/v1/me`
- **Quyền hạn (RBAC Policy):** `RequireRole (Authenticated)`
- **Quy tắc Sở hữu (Resource Ownership):** currentUserId (extracted strictly from HttpContext claims)
- **Controller:** `User.Api/Controllers/Public/MeController.cs`
- **Application Contract:** `User.Application/Services/Profile/IProfileService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Profile/ProfileRequests.cs`
- **Tên C# Record:** `UpdateProfileApiRequest`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed record UpdateProfileApiRequest(
    string? DisplayName = null,
    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null,
    string? AvatarUrl = null,
    string? Address = null,
    Gender? Gender = null,
    DateTimeOffset? DateOfBirth = null,
    string? Username = null
);
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "displayName": "John D.",
  "firstName": "Johnathan",
  "lastName": "Doe",
  "phoneNumber": "0912345678",
  "address": "456 Le Loi, District 1, Ho Chi Minh City",
  "gender": "Male",
  "dateOfBirth": "1995-05-20T00:00:00Z"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Profile/ProfileResponses.cs`
- **Tên C# Record:** `UserProfileDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
// Returns updated UserProfileDto
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "id": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "email": "buyer@example.com",
  "username": "buyer01",
  "firstName": "Johnathan",
  "lastName": "Doe",
  "displayName": "John D.",
  "phoneNumber": "0912345678",
  "address": "456 Le Loi, District 1, Ho Chi Minh City",
  "avatarUrl": "https://cdn.example.com/avatars/user1.png",
  "status": "Active",
  "roles": ["Buyer"],
  "permissions": ["view_products", "create_order"],
  "isSeller": false,
  "shopName": null,
  "reputationScore": 100,
  "trustLevel": "Standard",
  "createdAt": "2026-02-10T10:00:00Z"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `One or more validation errors occurred.` | Invalid format for phone number, gender or birth date |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Missing or expired token |
| `404 Not Found` | `USER_NOT_FOUND` | `User profile not found.` | User does not exist in DB |
| `409 Conflict` | `USERNAME_ALREADY_EXISTS` | `Username is already registered in the system.` | New username already taken |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `Gender.Male` | `1` | "Male" | Male gender option |
| `Gender.Female` | `2` | "Female" | Female gender option |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract currentUserId from token claims.
2. **Bước 2:** Retrieve user from database; ensure account is active.
3. **Bước 3:** Check username uniqueness if new username supplied.
4. **Bước 4:** Update user profile properties and record AuditLog for changes.
5. **Bước 5:** Save changes to database.
6. **Bước 6:** Return updated UserProfileDto with 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Me/PATCH_me.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Me/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
