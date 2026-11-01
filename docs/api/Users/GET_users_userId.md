# USER SERVICE - GET /api/v1/users/{userId}

> **Feature / Use Case:** Users — GetUserPublicProfile
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** AI Assistant (UserService Architecture Team)
> **Mô tả chức năng:** Retrieves the safe public profile of a user by ID (USER-07).

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/users/{userId}`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous`
- **Quy tắc Sở hữu (Resource Ownership):** Publicly readable safe profile (sensitive fields stripped)
- **Controller:** `User.Api/Controllers/Public/UsersController.cs`
- **Application Contract:** `User.Application/Services/Profile/IProfileService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `userId` | Route | `Guid` | Có | UUIDv4 format | Target user identifier (UUIDv4) |

### 2.3 Request Body Record DTO
- **Vị trí file:** `None (Route Parameter)`
- **Tên C# Record:** `None`
- **Khai báo C# & DataAnnotations:**
```csharp
// Route parameter only: Guid userId
```
- **JSON Request Body mẫu (camelCase):**
```json
{}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Profile/ProfileResponses.cs`
- **Tên C# Record:** `PublicUserProfileDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record PublicUserProfileDto(
    string Id,
    string? Username,
    string FirstName,
    string LastName,
    string? AvatarUrl,
    int ReputationScore,
    string TrustLevel,
    IReadOnlyList<string> Badges,
    bool IsSeller,
    DateTimeOffset JoinedAt
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "id": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "username": "johndoe",
  "firstName": "John",
  "lastName": "Doe",
  "avatarUrl": "https://cdn.example.com/avatars/user1.png",
  "reputationScore": 120,
  "trustLevel": "Standard",
  "badges": ["VerifiedBuyer"],
  "isSeller": true,
  "joinedAt": "2026-01-15T08:00:00Z"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Invalid user identifier format.` | Malformed GUID format in route |
| `404 Not Found` | `USER_NOT_FOUND` | `User account not found with the specified identifier.` | User does not exist or has been deleted |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `TrustLevel.Standard` | `2` | "Standard" | Standard reputable account |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Parse and validate userId GUID from route parameter.
2. **Bước 2:** Query user entity filtering out deleted and inactive records.
3. **Bước 3:** Map safe public fields to PublicUserProfileDto (omits email, phone, and hashed credentials).
4. **Bước 4:** Return 200 OK with PublicUserProfileDto.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Users/GET_users_userId.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Users/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
