# USER SERVICE - PUT /api/v1/admin/users/{userId}/roles

> **Feature / Use Case:** AdminUsers — AssignUserRoles
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Replaces the assigned roles of a user account with segregation of duties validation.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PUT`
- **Route Endpoint:** `/api/v1/admin/users/{userId}/roles`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin", "staff")`
- **Quy tắc Sở hữu (Resource Ownership):** Admin/Staff authority
- **Controller:** `User.Api/Controllers/Public/AdminUsersController.cs`
- **Application Contract:** `User.Application/Services/Admin/IAdminUserService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `userId` | Route | `Guid` | Có | UUIDv4 format | Target user ID |

### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Admin/AdminUserRequests.cs`
- **Tên C# Record:** `AssignAdminUserRolesDto`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed class AssignAdminUserRolesDto
{
    public List<string>? RoleCodes { get; set; }
    public List<string>? RoleIds { get; set; }

    [Required(ErrorMessage = "Reason is required.")]
    public string? Reason { get; set; }
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "roleCodes": ["SupportStaff", "Buyer"],
  "reason": "Promoted to customer support team"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Admin/AdminUserResponses.cs`
- **Tên C# Record:** `AdminUserRolesResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record AdminUserRolesResponseDto(
    string UserId,
    List<string> Roles,
    DateTimeOffset UpdatedAt
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "roles": ["SupportStaff", "Buyer"],
  "updatedAt": "2026-11-01T12:30:00Z"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Reason for role assignment is required.` | Missing reason string |
| `400 Bad Request` | `SOD_VIOLATION` | `A user cannot be assigned both Admin and SupportStaff roles simultaneously.` | Conflicting segregation of duties roles requested |
| `400 Bad Request` | `CANNOT_DEMOTE_LAST_ADMIN` | `Cannot remove Admin role from the last active Administrator in the system.` | Demotion of the sole system administrator |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Missing bearer token |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Missing admin or staff role |
| `404 Not Found` | `USER_NOT_FOUND` | `User was not found in the system.` | Target user does not exist |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract adminId from token claims.
2. **Bước 2:** Verify target user exists.
3. **Bước 3:** Validate segregation of duties (SoD) rules (e.g. Admin != SupportStaff).
4. **Bước 4:** Ensure last active Admin is not demoted.
5. **Bước 5:** Update user roles in database.
6. **Bước 6:** Record audit log via _dbContext.AddAuditLog(...).
7. **Bước 7:** Return AdminUserRolesResponseDto with 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/AdminUsers/PUT_admin_users_userId_roles.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/AdminUsers/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
