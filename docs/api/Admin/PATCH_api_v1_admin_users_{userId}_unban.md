# USER SERVICE - PATCH /api/v1/admin/users/{userId}/unban

> **Feature / Use Case:** ADMIN-06 (UnbanUser)
> **Trạng thái:** ✅ VERIFIED (Build 0 error, 286/286 tests pass)
> **Ngày tạo:** 2026-11-01
> **Tác giả:** AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PATCH`
- **Route Endpoint:** `/api/v1/admin/users/{userId}/unban` & `/api/v1/admin/user/{userId}/unban`
- **Quyền hạn (RBAC Policy):** `ADMIN` (`[RequireRole("admin")]`)
- **Quy tắc Sở hữu (Resource Ownership):** Thao tác đặc quyền Quản trị viên.
- **Controller:** `User.Api/Controllers/Public/AdminUsersController.cs`
- **Application Contract:** `User.Application/Services/Admin/IAdminUserService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `userId` | Route | `Guid` | Có | UUIDv4 format, non-empty | Định danh tài khoản cần mở khóa |

### 2.2 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Admin/AdminUserRequests.cs`
- **Tên C# Class:** `UnbanAdminUserDto`
- **Khai báo C# & Dual-Binding:**
```csharp
public sealed class UnbanAdminUserDto
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("unban_reason")]
    public string? UnbanReasonSnake { set => Reason ??= value; }

    [JsonPropertyName("unbanReason")]
    public string? UnbanReasonCamel { set => Reason ??= value; }
}
```
- **JSON Request Body mẫu (camelCase & snake_case):**
```json
{
  "reason": "Account unbanned following completed administrative review."
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần, KHÔNG bọc qua `ApiResponse<T>`.

- **Vị trí file:** `User.Application/Services/Admin/AdminUserResponses.cs`
- **Tên C# Record:** `AdminUserUnbannedResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record AdminUserUnbannedResponseDto(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("unbannedAt")] DateTimeOffset UnbannedAt,
    [property: JsonPropertyName("unbannedBy")] string UnbannedBy,
    [property: JsonPropertyName("message")] string Message
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "userId": "11111111-1111-1111-1111-111111111111",
  "status": "Active",
  "unbannedAt": "2026-11-01T10:35:00Z",
  "unbannedBy": "44444444-4444-4444-4444-444444444444",
  "message": "User account unbanned successfully."
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Target user ID is required.` | `userId` là `Guid.Empty` |
| `400 Bad Request` | `USER_NOT_BANNED` | `User account is not currently banned.` | `user.Status != UserStatus.Banned` |
| `400 Bad Request` | `USER_ALREADY_DELETED` | `User has already been deleted.` | `user.IsDeleted == true` |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication credentials are required.` | Thiếu header định danh caller |
| `403 Forbidden` | `FORBIDDEN` | `Administrator privileges are required.` | Caller không có role admin |
| `404 Not Found` | `USER_NOT_FOUND` | `User was not found in the system.` | Không tìm thấy `userId` trong DB |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `UserStatus.PendingVerification` | `0` | `"PendingVerification"` | Khôi phục nếu email chưa xác thực |
| `UserStatus.Active` | `1` | `"Active"` | Khôi phục nếu email đã xác thực |
| `UserStatus.Banned` | `2` | `"Banned"` | Trạng thái trước khi mở khóa |
| `AuditAction.UserUnban` | `2` | `"UserUnban"` | Hành động kiểm toán mở khóa tài khoản |

---

## 5. Kế hoạch Xử lý Kỹ thuật (Technical Implementation Plan)
1. Controller tiếp nhận request `PATCH /api/v1/admin/users/{userId}/unban`.
2. Kiểm tra `adminId` từ `HttpContext.GetUserId()`.
3. Gọi `IAdminUserService.UnbanUserAsync(userId, request, adminId, cancellationToken)`.
4. Service kiểm tra user tồn tại, chưa bị xóa và đang có trạng thái `Banned`.
5. Cập nhật `User`: khôi phục `Status = Active` (hoặc `PendingVerification`), xóa `BannedAt = null`, `BannedBy = null`, `BanReason = null`.
6. Xóa cờ đình chỉ và xóa lockout trên Redis (`_redisOtpService.UnbanUserAsync(userId)` và `ResetLoginAttemptsAsync`).
7. Ghi nhật ký kiểm toán `_dbContext.AddAuditLog(AuditAction.UserUnban, ...)`.
8. Lưu database và trả về `AdminUserUnbannedResponseDto`.
