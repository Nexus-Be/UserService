# USER SERVICE - GET /api/v1/me

> **Feature / Use Case:** Me — GetCurrentUserProfile
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** AI Assistant (UserService Architecture Team)
> **Mô tả chức năng:** Retrieves the complete profile of the currently authenticated user.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/me`
- **Quyền hạn (RBAC Policy):** `RequireRole (Authenticated)`
- **Quy tắc Sở hữu (Resource Ownership):** currentUserId (extracted strictly from HttpContext claims)
- **Controller:** `User.Api/Controllers/Public/MeController.cs`
- **Application Contract:** `User.Application/Services/Profile/IProfileService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `None (Header Bearer Token)`
- **Tên C# Record:** `None`
- **Khai báo C# & DataAnnotations:**
```csharp
// Identity extracted via HttpContext.GetUserId()
```
- **JSON Request Body mẫu (camelCase):**
```json
{}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Profile/ProfileResponses.cs`
- **Tên C# Record:** `UserProfileDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record UserProfileDto(
    string Id,
    string Email,
    string? Username,
    string FirstName,
    string LastName,
    string DisplayName,
    string? PhoneNumber,
    string? Address,
    string? AvatarUrl,
    string Status,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool IsSeller,
    string? ShopName,
    int ReputationScore,
    string TrustLevel,
    DateTimeOffset CreatedAt
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "id": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "email": "buyer@example.com",
  "username": "buyer01",
  "firstName": "John",
  "lastName": "Doe",
  "displayName": "John Doe",
  "phoneNumber": "0987654321",
  "address": "123 Main St, District 1, Ho Chi Minh City",
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
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Missing or expired Bearer token |
| `404 Not Found` | `USER_NOT_FOUND` | `User profile not found.` | User does not exist in DB |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `UserStatus.Active` | `1` | "Active" | Active user status |
| `TrustLevel.Standard` | `2` | "Standard" | Standard trust tier |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract currentUserId from JWT claims.
2. **Bước 2:** Retrieve user from database including roles, permissions, and seller profile.
3. **Bước 3:** Assemble UserProfileDto with full personal details.
4. **Bước 4:** Return 200 OK with UserProfileDto.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Me/GET_me.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Me/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
