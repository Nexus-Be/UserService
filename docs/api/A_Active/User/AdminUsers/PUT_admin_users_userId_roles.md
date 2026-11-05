# USER SERVICE - PUT /api/v1/admin/users/{userId}/roles

> **Feature / Use Case:** AdminUsers — AssignUserRoles
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Cập nhật:** 2026-11-05
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Quản trị viên (Admin) thay đổi vai trò chính của tài khoản người dùng theo quy chuẩn 1 User - 1 Primary Role và quy tắc phân tách nhiệm vụ (Segregation of Duties - SoD).

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PUT`
- **Route Endpoint:** `/api/v1/admin/users/{userId}/roles`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin")` (Chỉ dành riêng cho Administrator)
- **Quy tắc Sở hữu (Resource Ownership):** Thẩm quyền gán vai trò tối cao thuộc về Admin để bảo vệ tính bảo mật và phân quyền hệ thống.
- **Controller:** `User.Api/Controllers/Public/AdminUsersController.cs`
- **Application Contract:** `User.Application/Services/Admin/IAdminUserService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `userId` | Route | `Guid` | Có | Định dạng UUIDv4 hợp lệ | Định danh tài khoản người dùng cần cập nhật vai trò |

### 2.2 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Admin/AdminUserRequests.cs`
- **Tên C# Record:** `AssignAdminUserRolesDto`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed class AssignAdminUserRolesDto
{
    [JsonPropertyName("roleCodes")]
    public List<string>? RoleCodes { get; set; }

    [JsonPropertyName("roleIds")]
    public List<string>? RoleIds { get; set; }

    [Required(ErrorMessage = "Reason is required.")]
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
```
- **JSON Request Body mẫu (camelCase - Quy chuẩn 1 User 1 Role):**
```json
{
  "roleCodes": [
    "SupportStaff"
  ],
  "reason": "Promoted employee to Customer Support Operations team"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

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
  "roles": [
    "SupportStaff"
  ],
  "updatedAt": "2026-11-05T12:30:00Z"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Reason for role assignment is required.` | Thiếu lý do trong request body |
| `400 Bad Request` | `SOD_VIOLATION` | `A user cannot be assigned both Admin and SupportStaff roles simultaneously.` | Cố tình gán đồng thời vai trò Admin và SupportStaff |
| `400 Bad Request` | `MULTIPLE_ROLES_NOT_ALLOWED` | `A user can only be assigned exactly one primary role.` | Truyền nhiều hơn 1 vai trò trong danh sách gán |
| `400 Bad Request` | `CANNOT_ASSIGN_SELLER_ROLE_DIRECTLY` | `Seller role cannot be assigned directly. Use seller registration workflow.` | Cố tình gán vai trò Seller qua API admin thay vì quy trình Onboarding |
| `400 Bad Request` | `CANNOT_REVOKE_SELLER_ROLE_DIRECTLY` | `Seller role cannot be removed directly while seller profile is active or suspended.` | Cố tình xóa vai trò Seller khi gian hàng vẫn còn tồn tại |
| `400 Bad Request` | `CANNOT_DEMOTE_LAST_ADMIN` | `Cannot remove Admin role from the last active Administrator in the system.` | Tước quyền Admin của quản trị viên duy nhất còn lại |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Thiếu hoặc token không hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Người gọi không phải là Administrator (`admin`) |
| `404 Not Found` | `USER_NOT_FOUND` | `User was not found in the system.` | Người dùng mục tiêu không tồn tại trong hệ thống |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống cơ sở dữ liệu không mong muốn |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Trích xuất `adminId` từ JWT qua `HttpContext.GetUserId()` và xác nhận vai trò `admin`.
2. **Bước 2:** Xác minh sự tồn tại của `targetUserId` trong bảng `Users`.
3. **Bước 3:** Kiểm tra số lượng vai trò yêu cầu:
   - Nếu chứa cả `Admin` và `SupportStaff`: Ném ngoại lệ `SOD_VIOLATION`.
   - Nếu số vai trò `> 1`: Ném ngoại lệ `MULTIPLE_ROLES_NOT_ALLOWED`.
   - Nếu danh sách vai trò chứa `Seller`: Ném ngoại lệ `CANNOT_ASSIGN_SELLER_ROLE_DIRECTLY`.
4. **Bước 4:** Kiểm tra an toàn hạ tầng: Đảm bảo không hạ cấp người quản trị cuối cùng (`CANNOT_DEMOTE_LAST_ADMIN`).
5. **Bước 5:** Cập nhật liên kết khóa ngoại `RoleId` trong bảng `Users` và đồng bộ `UserRoles`.
6. **Bước 6:** Ghi nhật ký kiểm toán vào `AuditLogs` qua `_dbContext.AddAuditLog(...)`.
7. **Bước 7:** Trả về `AdminUserRolesResponseDto` với HTTP 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/A_Active/User/AdminUsers/PUT_admin_users_userId_roles.md`.
- [x] **Persistence:** Schema 1 User 1 Role qua `RoleId` và bảo vệ toàn vẹn dữ liệu đã hoàn thiện.
- [x] **Application:** Logic kiểm soát SoD và quy tắc 1 role đã triển khai trong `AdminUserService`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit tests bao phủ 1-user-1-role, SoD, bảo vệ Admin cuối cùng.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
