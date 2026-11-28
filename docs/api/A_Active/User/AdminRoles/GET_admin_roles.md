# USER SERVICE - GET /api/v1/admin/roles

> **Feature / Use Case:** AdminRoles — ListRoles
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Lists all system user roles available for assignment.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/admin/roles`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin", "staff")`
- **Quy tắc Sở hữu (Resource Ownership):** Admin/Staff authority
- **Controller:** `User.Api/Controllers/Public/AdminRolesController.cs`
- **Application Contract:** `User.Application/Services/Admin/IAdminUserService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `None`
- **Tên C# Record:** `None`
- **Khai báo C# & DataAnnotations:**
```csharp
// No request parameters
```
- **JSON Request Body mẫu (camelCase):**
```json
{}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Admin/AdminUserResponses.cs`
- **Tên C# Record:** `AdminRoleListResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record AdminRoleListResponseDto(
    List<AdminRoleDto> Roles
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "roles": [
    {
      "id": "1",
      "roleId": "1",
      "roleCode": "Buyer",
      "roleName": "Standard Buyer",
      "description": "Registered buyer capable of purchasing goods",
      "isSystemRole": true,
      "isActive": true,
      "permissionsCount": 5,
      "createdAt": "2026-01-01T00:00:00Z"
    },
    {
      "id": "2",
      "roleId": "2",
      "roleCode": "Seller",
      "roleName": "Merchant Seller",
      "description": "Verified seller capable of listing products",
      "isSystemRole": true,
      "isActive": true,
      "permissionsCount": 12,
      "createdAt": "2026-01-01T00:00:00Z"
    },
    {
      "id": "3",
      "roleId": "3",
      "roleCode": "SupportStaff",
      "roleName": "Support Staff",
      "description": "Operational staff handling customer support",
      "isSystemRole": true,
      "isActive": true,
      "permissionsCount": 20,
      "createdAt": "2026-01-01T00:00:00Z"
    },
    {
      "id": "4",
      "roleId": "4",
      "roleCode": "Admin",
      "roleName": "System Administrator",
      "description": "Full platform administration privileges",
      "isSystemRole": true,
      "isActive": true,
      "permissionsCount": 45,
      "createdAt": "2026-01-01T00:00:00Z"
    }
  ]
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Missing bearer token |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Caller is not Admin or Staff |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Verify caller has Admin or Staff role.
2. **Bước 2:** Query Roles table with associated permissions count.
3. **Bước 3:** Return AdminRoleListResponseDto containing all active roles with 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/AdminRoles/GET_admin_roles.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/AdminRoles/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
