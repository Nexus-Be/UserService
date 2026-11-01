# USER SERVICE - PATCH /api/v1/admin/users/{userId}/ban

> **Feature / Use Case:** ADMIN-05 (BanUser)
> **Trạng thái:** ✅ VERIFIED (Build 0 error, 286/286 tests pass)
> **Ngày tạo:** 2026-11-01
> **Tác giả:** AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PATCH`
- **Route Endpoint:** `/api/v1/admin/users/{userId}/ban` & `/api/v1/admin/user/{userId}/ban`
- **Quyền hạn (RBAC Policy):** `ADMIN` (`[RequireRole("admin")]`)
- **Quy tắc Sở hữu (Resource Ownership):** Thao tác đặc quyền Quản trị viên. Có rào chắn cấm Admin tự khóa chính mình (`adminId != targetUserId`) và cấm khóa Admin cuối cùng của hệ thống.
- **Controller:** `User.Api/Controllers/Public/AdminUsersController.cs`
- **Application Contract:** `User.Application/Services/Admin/IAdminUserService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `userId` | Route | `Guid` | Có | UUIDv4 format, non-empty | Định danh tài khoản cần khóa |

### 2.2 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Admin/AdminUserRequests.cs`
- **Tên C# Class:** `BanAdminUserDto`
- **Khai báo C# & Dual-Binding:**
```csharp
public sealed class BanAdminUserDto
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("ban_reason")]
    public string? BanReasonSnake { set => Reason ??= value; }

    [JsonPropertyName("banReason")]
    public string? BanReasonCamel { set => Reason ??= value; }
}
```
- **JSON Request Body mẫu (camelCase & snake_case):**
```json
{
  "reason": "Repeated fraudulent auction bidding activities violating terms of service."
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần, KHÔNG bọc qua `ApiResponse<T>`.

- **Vị trí file:** `User.Application/Services/Admin/AdminUserResponses.cs`
- **Tên C# Record:** `AdminUserBannedResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record AdminUserBannedResponseDto(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("banReason")] string BanReason,
    [property: JsonPropertyName("bannedAt")] DateTimeOffset BannedAt,
    [property: JsonPropertyName("bannedBy")] string BannedBy,
    [property: JsonPropertyName("revokedTokensCount")] int RevokedTokensCount,
    [property: JsonPropertyName("message")] string Message
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "userId": "11111111-1111-1111-1111-111111111111",
  "status": "Banned",
  "banReason": "Repeated fraudulent auction bidding activities violating terms of service.",
  "bannedAt": "2026-11-01T10:30:00Z",
  "bannedBy": "44444444-4444-4444-4444-444444444444",
  "revokedTokensCount": 2,
  "message": "User account banned successfully."
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Target user ID is required.` | `userId` là `Guid.Empty` |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Reason for banning user is required.` | Payload rỗng hoặc `reason` rỗng |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Ban reason cannot exceed 1000 characters.` | `reason.Length > 1000` |
| `400 Bad Request` | `CANNOT_BAN_SELF` | `Administrators cannot ban their own account.` | `adminId == targetUserId` |
| `400 Bad Request` | `CANNOT_BAN_LAST_ADMIN` | `Cannot ban the last active Administrator in the system.` | Target là admin duy nhất còn hoạt động |
| `400 Bad Request` | `USER_ALREADY_BANNED` | `User account is already banned.` | `user.Status == UserStatus.Banned` |
| `400 Bad Request` | `USER_ALREADY_DELETED` | `User has already been deleted.` | `user.IsDeleted == true` |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication credentials are required.` | Thiếu header định danh caller |
| `403 Forbidden` | `FORBIDDEN` | `Administrator privileges are required.` | Caller không có role admin |
| `404 Not Found` | `USER_NOT_FOUND` | `User was not found in the system.` | Không tìm thấy `userId` trong DB |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `UserStatus.PendingVerification` | `0` | `"PendingVerification"` | Chờ xác thực OTP |
| `UserStatus.Active` | `1` | `"Active"` | Đang hoạt động |
| `UserStatus.Banned` | `2` | `"Banned"` | Tài khoản bị khóa vi phạm |
| `UserStatus.Deleted` | `3` | `"Deleted"` | Tài khoản đã xóa mềm |
| `AuditAction.UserBan` | `1` | `"UserBan"` | Hành động kiểm toán khóa tài khoản |

---

## 5. Kế hoạch Xử lý Kỹ thuật (Technical Implementation Plan)
1. Controller tiếp nhận request `PATCH /api/v1/admin/users/{userId}/ban`.
2. Kiểm tra `adminId` từ `HttpContext.GetUserId()`.
3. Gọi `IAdminUserService.BanUserAsync(userId, request, adminId, cancellationToken)`.
4. Service thực hiện kiểm tra các rào chắn nghiệp vụ.
5. Cập nhật `User`: `Status = Banned`, `BannedAt`, `BannedBy`, `BanReason`.
6. Thu hồi toàn bộ active Refresh Tokens trong bảng `RefreshTokens`.
7. Ghi nhận thu hồi token và khóa tài khoản trên Redis (`user:tokens:revoked:{userId}`, `user:banned:{userId}`, `LockAccountAsync`, hủy OTPs).
8. Ghi nhật ký kiểm toán `_dbContext.AddAuditLog(AuditAction.UserBan, ...)`.
9. Lưu database và trả về `AdminUserBannedResponseDto`.
