# USER SERVICE - PATCH /api/v1/admin/users/{userId}/ban

> **Feature / Use Case:** AdminUsers — BanUser
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Cập nhật:** 2026-11-05
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Quản trị viên (Admin) áp dụng chế tài kỷ luật cao nhất: Khóa toàn diện tài khoản người dùng (`UserStatus = Banned`), thu hồi ngay lập tức toàn bộ phiên đăng nhập (JWT refresh tokens), vô hiệu hóa mọi quyền mua/bán và ghi nhận án phạt kèm hồ sơ bằng chứng vi phạm.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PATCH`
- **Route Endpoint:** `/api/v1/admin/users/{userId}/ban`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin")` (Chỉ dành riêng cho Administrator)
- **Quy tắc Sở hữu (Resource Ownership):** Thẩm quyền khóa tài khoản toàn diện thuộc về Admin cấp cao. Admin không được phép tự khóa tài khoản của chính mình hoặc khóa tài khoản của Admin khác.
- **Controller:** `User.Api/Controllers/Public/AdminUsersController.cs`
- **Application Contract:** `User.Application/Services/Admin/IAdminUserService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `userId` | Route | `Guid` | Có | Định dạng UUIDv4 hợp lệ | Định danh tài khoản người dùng bị khóa |

### 2.2 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Admin/AdminUserRequests.cs`
- **Tên C# Record:** `BanAdminUserDto`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed class BanAdminUserDto
{
    [Required(ErrorMessage = "Ban reason is required.")]
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("policyClauseCode")]
    public string? PolicyClauseCode { get; set; }

    [JsonPropertyName("evidenceUrls")]
    public List<string>? EvidenceUrls { get; set; }
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "reason": "Violating platform terms of service and confirmed fraudulent activities",
  "policyClauseCode": "POL_FRAUD_SECURITY_01",
  "evidenceUrls": [
    "https://cdn.example.com/reports/audit_evidence_01.pdf",
    "https://cdn.example.com/reports/chargeback_trace_02.png"
  ]
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Admin/AdminUserResponses.cs`
- **Tên C# Record:** `AdminUserBannedResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record AdminUserBannedResponseDto(
    string UserId,
    string Status,
    string BanReason,
    DateTimeOffset BannedAt,
    string BannedBy,
    int RevokedTokensCount,
    string Message
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "status": "Banned",
  "banReason": "Violating platform terms of service and confirmed fraudulent activities",
  "bannedAt": "2026-11-05T13:30:00Z",
  "bannedBy": "admin-guid-1234",
  "revokedTokensCount": 4,
  "message": "User account has been banned successfully."
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Ban reason is required.` | Chuỗi lý do khóa tài khoản bị để trống |
| `400 Bad Request` | `CANNOT_BAN_ADMIN` | `Administrators cannot ban fellow Admin accounts.` | Tài khoản mục tiêu giữ vai trò Quản trị viên (Admin) |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Thiếu hoặc token không hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Người gọi không phải là Administrator (`admin`) |
| `404 Not Found` | `USER_NOT_FOUND` | `User account not found.` | Tài khoản người dùng không tồn tại trong DB |
| `409 Conflict` | `USER_ALREADY_BANNED` | `User account is already banned.` | Tài khoản hiện tại đã ở trạng thái Banned |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống cơ sở dữ liệu không mong muốn |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `UserStatus.Banned` | `2` | `"Banned"` | Tài khoản bị đình chỉ toàn bộ quyền truy cập và thao tác |
| `TargetRole.Account` | `2` | `"Account"` | Chế tài án phạt áp dụng cho toàn thể tài khoản định danh |
| `PenaltyType.BanAccount` | `3` | `"BanAccount"` | Loại hình án phạt cấm vĩnh viễn hoặc vô thời hạn |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Trích xuất `adminId` từ JWT qua `HttpContext.GetUserId()` và xác thực vai trò `admin`.
2. **Bước 2:** Truy vấn người dùng mục tiêu trong bảng `Users`.
3. **Bước 3:** Kiểm tra người dùng mục tiêu có nắm vai trò `Admin` hay không; ném `CANNOT_BAN_ADMIN` nếu vi phạm.
4. **Bước 4:** Kiểm tra trạng thái hiện tại: Ném `USER_ALREADY_BANNED` nếu đã bị khóa.
5. **Bước 5:** Cập nhật `Status = UserStatus.Banned`, `BannedAt = DateTimeOffset.UtcNow`, `BanReason = dto.Reason`.
6. **Bước 6:** Hủy bỏ (Revoke) tất cả các refresh token đang hoạt động trong bảng `UserSessions`.
7. **Bước 7:** Tạo biên bản xử phạt `UserPenalty` với `TargetRole = TargetRole.Account`, `PenaltyApplied = PenaltyType.BanAccount`, lưu trữ danh sách `EvidenceUrls`.
8. **Bước 8:** Ghi nhật ký kiểm toán `_dbContext.AddAuditLog(...)` và trả về `AdminUserBannedResponseDto` với HTTP 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/A_Active/User/AdminUsers/PATCH_admin_users_userId_ban.md`.
- [x] **Persistence:** Cột `EvidenceUrls`, `PolicyClauseCode`, `TargetRole` trong bảng `UserPenalties` đã được cấu hình đầy đủ.
- [x] **Application:** Logic Ban tài khoản đã hoàn thiện trong `AdminUserService.BanUserAsync`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit tests bao phủ cấm ban Admin, ban thành công và hủy session.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
