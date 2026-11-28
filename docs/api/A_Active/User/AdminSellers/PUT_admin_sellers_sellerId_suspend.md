# USER SERVICE - PUT /api/v1/admin/sellers/{sellerId}/suspend

> **Feature / Use Case:** AdminSellers — SuspendSeller
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-05
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Quản trị viên (Admin) hoặc Nhân viên hỗ trợ (Support Staff) tạm đình chỉ quyền bán hàng của gian hàng (ShopStatus = Suspended). Quyền mua sắm cá nhân của người bán vẫn được bảo toàn.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PUT`
- **Route Endpoint:** `/api/v1/admin/sellers/{sellerId}/suspend`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin", "staff")`
- **Quy tắc Sở hữu (Resource Ownership):** Admin và Support Staff có toàn quyền đình chỉ gian hàng vi phạm chính sách sàn thương mại điện tử.
- **Controller:** `User.Api/Controllers/Public/AdminSellersController.cs`
- **Application Contract:** `User.Application/Services/Seller/ISellerService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `sellerId` | Route | `Guid` | Có | Định dạng UUIDv4 hợp lệ | Định danh hồ sơ người bán (`SellerProfiles.Id`) |

### 2.2 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Seller/SellerRequests.cs`
- **Tên C# Record:** `SuspendSellerDto`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed class SuspendSellerDto
{
    [Required(ErrorMessage = "Reason is required.")]
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("durationDays")]
    public int? DurationDays { get; set; }

    [JsonPropertyName("policyClauseCode")]
    public string? PolicyClauseCode { get; set; }

    [JsonPropertyName("evidenceUrls")]
    public List<string>? EvidenceUrls { get; set; }
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "reason": "Repeated order cancellation violations and late dispatch",
  "durationDays": 14,
  "policyClauseCode": "POL_SELLER_CANCELLATION",
  "evidenceUrls": [
    "https://cdn.example.com/reports/evidence1.jpg",
    "https://cdn.example.com/reports/evidence2.jpg"
  ]
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Seller/SellerResponses.cs`
- **Tên C# Record:** `SuspendSellerResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record SuspendSellerResponseDto(
    string SellerId,
    string ShopName,
    string Status,
    DateTimeOffset SuspendedAt,
    DateTimeOffset? ExpiresAt,
    string Reason
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "sellerId": "11111111-1111-1111-1111-111111111111",
  "shopName": "Green Garden Store",
  "status": "Suspended",
  "suspendedAt": "2026-11-05T10:00:00Z",
  "expiresAt": "2026-11-19T10:00:00Z",
  "reason": "Repeated order cancellation violations and late dispatch"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Reason is required.` | Thiếu lý do đình chỉ trong request body |
| `400 Bad Request` | `SELLER_ALREADY_SUSPENDED` | `Seller profile is already suspended.` | Gian hàng hiện tại đã ở trạng thái Suspended |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Thiếu hoặc token không hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Người gọi không có vai trò Admin hoặc Staff |
| `404 Not Found` | `SELLER_NOT_FOUND` | `Seller was not found in the system.` | Không tìm thấy hồ sơ người bán theo ID |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống cơ sở dữ liệu không mong muốn |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `SellerProfileStatus.Suspended` | `3` | `"Suspended"` | Gian hàng bị tạm ngưng hoạt động bán lẻ |
| `TargetRole.Seller` | `1` | `"Seller"` | Án phạt nhắm mục tiêu đến vai trò người bán |
| `PenaltyType.SuspendSeller` | `2` | `"SuspendSeller"` | Loại hình kỷ luật đình chỉ quyền bán lẻ |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Trích xuất `actorId` từ JWT qua `HttpContext.GetUserId()` và kiểm tra vai trò `admin`/`staff`.
2. **Bước 2:** Truy vấn hồ sơ `SellerProfiles` theo `sellerId`.
3. **Bước 3:** Kiểm tra nếu `seller.Status == SellerProfileStatus.Suspended` thì ném `BusinessRuleValidationException("SELLER_ALREADY_SUSPENDED", ...)`.
4. **Bước 4:** Cập nhật `seller.Status = SellerProfileStatus.Suspended`, vô hiệu hóa quyền bán (`CanSell = false`).
5. **Bước 5:** Tạo biên bản xử phạt `UserPenalty` với `TargetRole = TargetRole.Seller`, `PenaltyApplied = PenaltyType.SuspendSeller`.
6. **Bước 6:** Ghi nhật ký kiểm toán qua `_dbContext.AddAuditLog(...)`.
7. **Bước 7:** Lưu thay đổi vào DB và trả về `SuspendSellerResponseDto` với HTTP 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/A_Active/User/AdminSellers/PUT_admin_sellers_sellerId_suspend.md`.
- [x] **Persistence:** Entity `UserPenalty` và `SellerProfile` đã hỗ trợ `Status = 3 (Suspended)`.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Seller/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit tests bao phủ happy path và các mã lỗi trong Error Matrix (`AdminSellersControllerRestTests.cs`).
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
