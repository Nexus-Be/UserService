# USER SERVICE - GET /api/v1/admin/users/{userId}

> **Feature / Use Case:** AdminUsers — GetUserDetail
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** AI Assistant (UserService Architecture Team)
> **Mô tả chức năng:** Retrieves the administrative detail of a single user account.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/admin/users/{userId}`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin", "staff")`
- **Quy tắc Sở hữu (Resource Ownership):** Admin/Staff authority
- **Controller:** `User.Api/Controllers/Public/AdminUsersController.cs`
- **Application Contract:** `User.Application/Services/Admin/IAdminUserService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `userId` | Route | `Guid` | Có | UUIDv4 format | Target user identifier |

### 2.3 Request Body Record DTO
- **Vị trí file:** `None (Route Parameter)`
- **Tên C# Record:** `None`
- **Khai báo C# & DataAnnotations:**
```csharp
// Route parameter: Guid userId
```
- **JSON Request Body mẫu (camelCase):**
```json
{}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Admin/AdminUserResponses.cs`
- **Tên C# Record:** `AdminUserDetailDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record AdminUserDetailDto(
    string Id,
    string UserId,
    string Email,
    string? Username,
    string DisplayName,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? IdentifyNumber,
    string? AvatarUrl,
    string Gender,
    DateTimeOffset? DateOfBirth,
    string? Address,
    string Status,
    bool IsSeller,
    int ReputationScore,
    string TrustLevel,
    List<string> Roles,
    List<string> Permissions,
    AdminSellerProfileDetailDto? SellerProfile,
    List<AdminUserReputationLogDto> ReputationLogs,
    DateTimeOffset? BannedAt,
    string? BanReason,
    DateTimeOffset? EmailVerifiedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "id": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "email": "buyer@example.com",
  "username": "buyer01",
  "displayName": "John Doe",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "0987654321",
  "identifyNumber": "012345678901",
  "avatarUrl": "https://cdn.example.com/avatars/user1.png",
  "gender": "Male",
  "dateOfBirth": "1995-05-20T00:00:00Z",
  "address": "123 Main St",
  "status": "Active",
  "isSeller": false,
  "reputationScore": 100,
  "trustLevel": "Standard",
  "roles": ["Buyer"],
  "permissions": ["view_products", "create_order"],
  "sellerProfile": null,
  "reputationLogs": [],
  "bannedAt": null,
  "banReason": null,
  "emailVerifiedAt": "2026-02-10T10:05:00Z",
  "createdAt": "2026-02-10T10:00:00Z",
  "updatedAt": null
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Target user identifier is required.` | Invalid GUID format |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Missing bearer token |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Missing admin or staff role |
| `404 Not Found` | `USER_NOT_FOUND` | `User account not found.` | User does not exist in DB |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Validate userId GUID parameter.
2. **Bước 2:** Query user including roles, permissions, seller profile, and reputation logs.
3. **Bước 3:** Assemble comprehensive AdminUserDetailDto.
4. **Bước 4:** Return 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/AdminUsers/GET_admin_users_userId.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/AdminUsers/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
