# USER SERVICE - GET /api/v1/admin/penalties

> **Feature / Use Case:** AdminPenalties — GetPenalties
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-05
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Quản trị viên (Admin) hoặc Nhân viên hỗ trợ (Support Staff) tra cứu danh sách biên bản xử phạt vi phạm toàn hệ thống với phân trang cơ sở dữ liệu và các bộ lọc linh hoạt.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/admin/penalties`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin", "staff")`
- **Quy tắc Sở hữu (Resource Ownership):** Admin và Staff có quyền giám sát toàn bộ lịch sử vi phạm và chế tài xử phạt trên sàn.
- **Controller:** `User.Api/Controllers/Public/AdminPenaltiesController.cs`
- **Application Contract:** `User.Application/Services/Admin/IAdminPenaltyService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `page` | Query | `int` | Không | Min: 1, Mặc định: 1 | Trang hiện tại |
| `limit` | Query | `int` | Không | Min: 1, Max: 100, Mặc định: 20 | Số lượng bản ghi mỗi trang |
| `userId` | Query | `Guid?` | Không | Định dạng UUIDv4 hợp lệ | Lọc theo ID người dùng bị phạt |
| `targetRole` | Query | `TargetRole?` | Không | `Buyer`, `Seller`, `Account` | Lọc theo vai trò mục tiêu chịu chế tài |
| `status` | Query | `PenaltyStatus?` | Không | `Active`, `Expired`, `Revoked` | Lọc theo trạng thái hiệu lực của án phạt |

### 2.2 Request Body Record DTO
*Endpoint này không sử dụng Request Body.*

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần đóng gói trong `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Admin/AdminPenaltyDtos.cs`
- **Tên C# Record:** `PagedData<AdminPenaltyListItemDto>`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record AdminPenaltyListItemDto(
    string Id,
    string UserId,
    string? UserEmail,
    TargetRole TargetRole,
    PenaltyType PenaltyApplied,
    PenaltyViolationType ViolationType,
    PenaltyStatus Status,
    string Reason,
    string? PolicyClauseCode,
    int PointsDeducted,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "items": [
    {
      "id": "e9fdfbe3-03ca-4040-aa50-5b57a9767a78",
      "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
      "userEmail": "seller@greengarden.com",
      "targetRole": "Seller",
      "penaltyApplied": "Restriction",
      "violationType": "CancellationWithoutReason",
      "status": "Active",
      "reason": "Repeated order cancellation violations",
      "policyClauseCode": "POL_CANCEL_01",
      "pointsDeducted": 10,
      "issuedAt": "2026-11-05T08:00:00Z",
      "expiresAt": "2026-11-19T08:00:00Z"
    }
  ],
  "totalItems": 1,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1,
  "hasNext": false,
  "hasPrevious": false
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Thiếu hoặc token không hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Người gọi không có vai trò Admin hoặc Staff |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống cơ sở dữ liệu không mong muốn |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `TargetRole.Buyer` | `0` | `"Buyer"` | Án phạt tác động đến vai trò mua hàng |
| `TargetRole.Seller` | `1` | `"Seller"` | Án phạt tác động đến vai trò bán hàng |
| `TargetRole.Account` | `2` | `"Account"` | Khóa toàn bộ tài khoản người dùng |
| `PenaltyStatus.Active` | `0` | `"Active"` | Án phạt đang có hiệu lực áp dụng |
| `PenaltyStatus.Expired` | `1` | `"Expired"` | Án phạt đã hết thời hạn áp dụng |
| `PenaltyStatus.Revoked` | `2` | `"Revoked"` | Án phạt đã được Admin thu hồi |
| `PenaltyType.Warning` | `0` | `"Warning"` | Cảnh cáo vi phạm |
| `PenaltyType.DeductPoints` | `1` | `"DeductPoints"` | Trừ điểm uy tín |
| `PenaltyType.Restriction` | `2` | `"Restriction"` | Hạn chế thao tác (ví dụ: đình chỉ bán) |
| `PenaltyType.BanAccount` | `3` | `"BanAccount"` | Khóa toàn bộ tài khoản |
| `PenaltyType.Other` | `4` | `"Other"` | Biện pháp chế tài khác |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Kiểm tra quyền hạn `admin`/`staff` từ JWT token.
2. **Bước 2:** Xây dựng truy vấn EF Core trên bảng `UserPenalties` kết hợp `Users`.
3. **Bước 3:** Áp dụng bộ lọc `userId`, `targetRole`, `status` nếu có.
4. **Bước 4:** Thực hiện phân trang cơ sở dữ liệu qua `.Skip((page - 1) * limit).Take(limit)`.
5. **Bước 5:** Ánh xạ dữ liệu sang `AdminPenaltyListItemDto` và đóng gói vào `PagedData<T>`.
6. **Bước 6:** Trả về kết quả HTTP 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/A_Active/User/AdminPenalties/GET_admin_penalties.md`.
- [x] **Persistence:** Bảng `UserPenalties` và partial index `IX_UserPenalties_Active_Evaluation` đã hỗ trợ truy vấn tối ưu.
- [x] **Application:** Interface `IAdminPenaltyService`, Service `AdminPenaltyService` đã viết xong trong `Services/Admin/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit tests bao phủ phân trang và bộ lọc.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
