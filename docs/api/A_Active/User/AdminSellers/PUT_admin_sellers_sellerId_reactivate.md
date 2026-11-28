# USER SERVICE - PUT /api/v1/admin/sellers/{sellerId}/reactivate

> **Feature / Use Case:** AdminSellers — ReactivateSeller
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-05
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Quản trị viên (Admin) hoặc Nhân viên hỗ trợ (Support Staff) khôi phục quyền bán hàng cho gian hàng đã bị đình chỉ (ShopStatus = Active). Khôi phục quyền đăng bán sản phẩm và vận hành đơn hàng.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PUT`
- **Route Endpoint:** `/api/v1/admin/sellers/{sellerId}/reactivate`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin", "staff")`
- **Quy tắc Sở hữu (Resource Ownership):** Admin và Support Staff có thẩm quyền khôi phục gian hàng sau khi hoàn tất thời hạn kỷ luật hoặc xử lý khiếu nại thành công.
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
- **Tên C# Record:** `ReactivateSellerDto` (Tùy chọn, cho phép rỗng)
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed class ReactivateSellerDto
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "reason": "Seller completed compliance training and fulfilled all backlogged dispute resolutions."
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Seller/SellerResponses.cs`
- **Tên C# Record:** `ReactivateSellerResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record ReactivateSellerResponseDto(
    string SellerId,
    string ShopName,
    string Status,
    DateTimeOffset ReactivatedAt,
    string Reason
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "sellerId": "11111111-1111-1111-1111-111111111111",
  "shopName": "Green Garden Store",
  "status": "Active",
  "reactivatedAt": "2026-11-05T12:00:00Z",
  "reason": "Seller completed compliance training and fulfilled all backlogged dispute resolutions."
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `SELLER_NOT_SUSPENDED` | `Seller profile is not currently suspended.` | Gian hàng không ở trạng thái Suspended |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Thiếu hoặc token không hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Người gọi không có vai trò Admin hoặc Staff |
| `404 Not Found` | `SELLER_NOT_FOUND` | `Seller was not found in the system.` | Không tìm thấy hồ sơ người bán theo ID |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống cơ sở dữ liệu không mong muốn |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `SellerProfileStatus.Active` | `1` | `"Active"` | Gian hàng hoạt động bình thường, mở khóa quyền bán |
| `PenaltyStatus.Revoked` | `2` | `"Revoked"` | Trạng thái án phạt được đánh dấu thu hồi |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Trích xuất `actorId` từ JWT qua `HttpContext.GetUserId()` và kiểm tra vai trò `admin`/`staff`.
2. **Bước 2:** Truy vấn hồ sơ `SellerProfiles` theo `sellerId`.
3. **Bước 3:** Kiểm tra nếu `seller.Status != SellerProfileStatus.Suspended` thì ném `BusinessRuleValidationException("SELLER_NOT_SUSPENDED", ...)`.
4. **Bước 4:** Cập nhật `seller.Status = SellerProfileStatus.Active`, khôi phục quyền bán hàng (`CanSell = true`).
5. **Bước 5:** Hủy hiệu lực (Revoke/Expire) các án phạt đình chỉ `TargetRole = Seller` đang hoạt động trong bảng `UserPenalties`.
6. **Bước 6:** Ghi nhật ký kiểm toán qua `_dbContext.AddAuditLog(...)`.
7. **Bước 7:** Lưu thay đổi vào DB và trả về `ReactivateSellerResponseDto` với HTTP 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/A_Active/User/AdminSellers/PUT_admin_sellers_sellerId_reactivate.md`.
- [x] **Persistence:** Entity `UserPenalty` và `SellerProfile` đã hỗ trợ chuyển đổi trạng thái `Active`.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Seller/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit tests bao phủ happy path và các mã lỗi trong Error Matrix (`AdminSellersControllerRestTests.cs`).
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
