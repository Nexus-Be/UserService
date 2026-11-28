# USER SERVICE - GET /api/v1/admin/penalties/{penaltyId}

> **Feature / Use Case:** AdminPenalties — GetPenaltyDetail
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-05
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Quản trị viên (Admin) hoặc Nhân viên hỗ trợ (Support Staff) tra cứu chi tiết một biên bản xử phạt cụ thể bao gồm danh sách bằng chứng vi phạm, điều khoản chính sách và thông tin thu hồi án phạt (nếu có).

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/admin/penalties/{penaltyId}`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin", "staff")`
- **Quy tắc Sở hữu (Resource Ownership):** Admin và Staff có quyền xem chi tiết án phạt và tài liệu bằng chứng để xử lý khiếu nại.
- **Controller:** `User.Api/Controllers/Public/AdminPenaltiesController.cs`
- **Application Contract:** `User.Application/Services/Admin/IAdminPenaltyService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `penaltyId` | Route | `Guid` | Có | Định dạng UUIDv4 hợp lệ | Định danh biên bản xử phạt (`UserPenalties.Id`) |

### 2.2 Query Parameters & Request Body
*Endpoint này không sử dụng Query Parameters và Request Body.*

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Admin/AdminPenaltyDtos.cs`
- **Tên C# Record:** `AdminPenaltyDetailDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record AdminPenaltyDetailDto(
    string Id,
    string UserId,
    string? UserEmail,
    string? UserFullName,
    TargetRole TargetRole,
    PenaltyType PenaltyApplied,
    PenaltyViolationType ViolationType,
    PenaltyStatus Status,
    string Reason,
    string? PolicyClauseCode,
    List<string> EvidenceUrls,
    int PointsDeducted,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt,
    string? CreatedBy,
    string? RevokedBy,
    DateTimeOffset? RevokedAt,
    string? RevokeReason
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "id": "e9fdfbe3-03ca-4040-aa50-5b57a9767a78",
  "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "userEmail": "seller@greengarden.com",
  "userFullName": "Nguyen Van A",
  "targetRole": "Seller",
  "penaltyApplied": "Restriction",
  "violationType": "CancellationWithoutReason",
  "status": "Active",
  "reason": "Exceeded maximum cancellation threshold",
  "policyClauseCode": "POL_CANCEL_01",
  "evidenceUrls": [
    "https://cdn.example.com/evidence/order_cancel_1.png",
    "https://cdn.example.com/evidence/chat_log_refusal.png"
  ],
  "pointsDeducted": 10,
  "issuedAt": "2026-11-05T08:00:00Z",
  "expiresAt": "2026-11-19T08:00:00Z",
  "createdBy": "admin-uuid-1111",
  "revokedBy": null,
  "revokedAt": null,
  "revokeReason": null
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Target penalty ID is required.` | UUID không hợp lệ hoặc để trống |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Thiếu hoặc token không hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Người gọi không có vai trò Admin hoặc Staff |
| `404 Not Found` | `PENALTY_NOT_FOUND` | `Penalty record was not found in the system.` | Không tìm thấy biên bản xử phạt theo ID |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống cơ sở dữ liệu không mong muốn |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Trích xuất thông tin người dùng từ JWT qua `HttpContext.GetUserId()` và kiểm tra vai trò `admin`/`staff`.
2. **Bước 2:** Tìm biên bản xử phạt trong bảng `UserPenalties` kèm `Users` theo `penaltyId`.
3. **Bước 3:** Ném `ResourceNotFoundException("PENALTY_NOT_FOUND", ...)` nếu không tìm thấy bản ghi.
4. **Bước 4:** Ánh xạ thực thể sang `AdminPenaltyDetailDto` (bao gồm deserialize `EvidenceUrls`).
5. **Bước 5:** Trả về kết quả HTTP 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/A_Active/User/AdminPenalties/GET_admin_penalties_penaltyId.md`.
- [x] **Persistence:** Entity `UserPenalty` với đầy đủ các cột bằng chứng và thu hồi đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Admin/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit tests bao phủ tìm kiếm chi tiết và xử lý lỗi không tìm thấy.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
