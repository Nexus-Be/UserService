# USER SERVICE - GET /api/v1/admin/users

> **Feature / Use Case:** AdminUsers — ListUsers
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Lists user accounts with database-level pagination, search keyword and filters.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/admin/users`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin", "staff")`
- **Quy tắc Sở hữu (Resource Ownership):** Admin/Staff authority
- **Controller:** `User.Api/Controllers/Public/AdminUsersController.cs`
- **Application Contract:** `User.Application/Services/Admin/IAdminUserService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.2 Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `page` | Query | `int` | Không | Page number (Default: 1, Min: 1) | Tham số truy vấn |
| `pageSize` | Query | `int` | Không | Items per page (Default: 20, Max: 100) | Tham số truy vấn |
| `keyword` | Query | `string` | Không | Search by email, name or username | Tham số truy vấn |
| `role` | Query | `string` | Không | Filter by role code (e.g. Buyer, Seller, Admin) | Tham số truy vấn |
| `status` | Query | `string` | Không | Filter by status (Active, PendingVerification, Banned) | Tham số truy vấn |

### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Api/Common/PageQuery.cs`
- **Tên C# Record:** `PageQuery`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed record PageQuery(int Page = 1, int PageSize = 20);
```
- **JSON Request Body mẫu (camelCase):**
```json
{}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Common/Models/PagedData.cs`
- **Tên C# Record:** `PagedData<AdminUserListItemDto>`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record PagedData<T>(
    IReadOnlyList<T> Items,
    int TotalItems,
    int Page,
    int PageSize,
    int TotalPages,
    bool HasNext,
    bool HasPrevious
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "items": [
    {
      "id": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
      "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
      "username": "buyer01",
      "email": "buyer@example.com",
      "displayName": "John Doe",
      "firstName": "John",
      "lastName": "Doe",
      "phoneNumber": "0987654321",
      "avatarUrl": "https://cdn.example.com/avatars/user1.png",
      "address": "123 Main St",
      "reputationScore": 100,
      "trustLevel": "Standard",
      "isSeller": false,
      "roles": ["Buyer"],
      "status": "Active",
      "createdAt": "2026-02-10T10:00:00Z"
    }
  ],
  "totalItems": 150,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8,
  "hasNext": true,
  "hasPrevious": false
}
```


> [!NOTE]
> **Cơ chế Kế thừa Vai trò (Role Hierarchy):**
> Mỗi đối tượng người dùng trong danh sách trả về mảng `roles` bao gồm cả vai trò chính và các vai trò kế thừa (Role Hierarchy).

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Missing bearer token |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Missing admin or staff role |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Normalize pagination parameters (safePage, safePageSize).
2. **Bước 2:** Build IQueryable query with keyword search, role filter and status filter.
3. **Bước 3:** Execute database CountAsync() for total items count.
4. **Bước 4:** Execute database Skip().Take() pagination query.
5. **Bước 5:** Wrap results into PagedData<AdminUserListItemDto>.
6. **Bước 6:** Return 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/AdminUsers/GET_admin_users.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/AdminUsers/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
