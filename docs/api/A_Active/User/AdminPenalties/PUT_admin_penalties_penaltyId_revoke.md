# USER SERVICE - PUT /api/v1/admin/penalties/{penaltyId}/revoke

> **Feature / Use Case:** AdminPenalties — RevokePenalty
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-05
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Quản trị viên cấp cao (Admin) thu hồi hoặc hủy bỏ một án phạt đang có hiệu lực. Hệ thống tự động khôi phục quyền vận hành tương ứng (ví dụ: mở khóa quyền bán hàng hoặc gỡ trạng thái Banned nếu không còn vi phạm nào khác).

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PUT`
- **Route Endpoint:** `/api/v1/admin/penalties/{penaltyId}/revoke`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin")` (Chỉ dành riêng cho Administrator)
- **Quy tắc Sở hữu (Resource Ownership):** Thẩm quyền thu hồi án phạt thuộc về Admin cấp cao để đảm bảo tính nghiêm minh và phòng chống lạm quyền.
- **Controller:** `User.Api/Controllers/Public/AdminPenaltiesController.cs`
- **Application Contract:** `User.Application/Services/Admin/IAdminPenaltyService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `penaltyId` | Route | `Guid` | Có | Định dạng UUIDv4 hợp lệ | Định danh biên bản xử phạt (`UserPenalties.Id`) |

### 2.2 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Admin/AdminPenaltyDtos.cs`
- **Tên C# Record:** `RevokePenaltyDto`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed class RevokePenaltyDto
{
    [Required(ErrorMessage = "Reason is required.")]
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "reason": "Merchant provided valid logistics carrier proof showing buyer-initiated refusal rather than seller cancellation."
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Admin/AdminPenaltyDtos.cs`
- **Tên C# Record:** `AdminPenaltyRevokedResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record AdminPenaltyRevokedResponseDto(
    string PenaltyId,
    string UserId,
    PenaltyStatus Status,
    DateTimeOffset RevokedAt,
    string RevokeReason,
    string RestoredOperationalState
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "penaltyId": "e9fdfbe3-03ca-4040-aa50-5b57a9767a78",
  "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "status": "Revoked",
  "revokedAt": "2026-11-05T12:00:00Z",
  "revokeReason": "Merchant provided valid logistics carrier proof showing buyer-initiated refusal rather than seller cancellation.",
  "restoredOperationalState": "Seller privileges restored to Active"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Reason is required.` | Thiếu lý do thu hồi án phạt |
| `400 Bad Request` | `PENALTY_NOT_ACTIVE` | `Only active penalties can be revoked.` | Án phạt đã hết hạn hoặc đã bị thu hồi trước đó |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Thiếu hoặc token không hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Người gọi không phải là Administrator (`admin`) |
| `404 Not Found` | `PENALTY_NOT_FOUND` | `Penalty record was not found in the system.` | Không tìm thấy biên bản xử phạt theo ID |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống cơ sở dữ liệu không mong muốn |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Trích xuất `adminId` từ JWT qua `HttpContext.GetUserId()` và kiểm tra vai trò `admin`.
2. **Bước 2:** Truy vấn biên bản xử phạt trong bảng `UserPenalties` theo `penaltyId`.
3. **Bước 3:** Kiểm tra nếu `penalty.Status != PenaltyStatus.Active` thì ném `BusinessRuleValidationException("PENALTY_NOT_ACTIVE", ...)`.
4. **Bước 4:** Cập nhật `Status = PenaltyStatus.Revoked`, `RevokedAt = DateTimeOffset.UtcNow`, `RevokedBy = adminId`, `RevokeReason = dto.Reason`.
5. **Bước 5:** Đánh giá và khôi phục trạng thái hoạt động: nếu là phạt `TargetRole.Seller` và không còn án phạt nào khác đang hiệu lực thì khôi phục `SellerProfiles.Status = Active`.
6. **Bước 6:** Ghi nhật ký kiểm toán qua `_dbContext.AddAuditLog(...)`.
7. **Bước 7:** Lưu thay đổi vào DB và trả về `AdminPenaltyRevokedResponseDto` với HTTP 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/A_Active/User/AdminPenalties/PUT_admin_penalties_penaltyId_revoke.md`.
- [x] **Persistence:** Entity `UserPenalty` với các trường thu hồi và quan hệ kiểm toán đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Admin/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit tests bao phủ happy path thu hồi và các validation vi phạm trạng thái.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
