# USER SERVICE - GET /api/v1/admin/sellers/{sellerId}

> **Feature / Use Case:** AdminSellers — GetAdminSellerProfile
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Cập nhật:** 2026-11-05
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Quản trị viên (Admin) hoặc Nhân viên hỗ trợ (Staff) tra cứu thông tin hồ sơ chi tiết, giấy tờ KYC, thông tin kho hàng và điểm uy tín của gian hàng người bán.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/admin/sellers/{sellerId}`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin", "staff")`
- **Quy tắc Sở hữu (Resource Ownership):** Admin và Support Staff được quyền truy cập toàn bộ hồ sơ người bán phục vụ thẩm định và kiểm soát.
- **Controller:** `User.Api/Controllers/Public/AdminSellersController.cs`
- **Application Contract:** `User.Application/Services/Seller/ISellerService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `sellerId` | Route | `Guid` | Có | Định dạng UUIDv4 hợp lệ, không được để trống | Định danh hồ sơ người bán (`SellerProfiles.Id`) hoặc ID người dùng (`Users.Id`) |

### 2.2 Query Parameters & Request Body
*Endpoint này không có Query Parameters và không có Request Body.*

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Seller/SellerResponses.cs`
- **Tên C# Record:** `AdminSellerProfileResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record AdminSellerProfileResponseDto(
    string SellerId,
    string ShopId,
    string UserId,
    string ShopName,
    string? AvatarUrl,
    string? BusinessEmail,
    string Hotline,
    string? Description,
    string PickupAddress,
    string WarehouseAddress,
    int ProvinceId,
    int DistrictId,
    string WardCode,
    string? IdentityCardNumber,
    string? IdCardFrontUrl,
    string? IdCardBackUrl,
    string? BusinessLicenseUrl,
    string OwnerFullName,
    string OwnerEmail,
    string? OwnerPhoneNumber,
    string OwnerGender,
    DateTimeOffset? OwnerDateOfBirth,
    string? OwnerAddress,
    SellerBankInfoDto BankInfo,
    SellerSettlementInfoDto SettlementInfo,
    double RatingAvg,
    int ReputationScore,
    string TrustLevel,
    int TotalPositiveReviews,
    int TotalNegativeReviews,
    int PenaltyCount,
    string Status,
    string? RejectionReason,
    string? ReviewedBy,
    string? ReviewedByName,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "sellerId": "11111111-1111-1111-1111-111111111111",
  "shopId": "SHOP_11111111",
  "userId": "22222222-2222-2222-2222-222222222222",
  "shopName": "Green Garden Store",
  "avatarUrl": "https://cdn.example.com/avatars/shop1.png",
  "businessEmail": "seller@greengarden.com",
  "hotline": "0901234567",
  "description": "Chuyên cung cấp cây cảnh và nông sản sạch",
  "pickupAddress": "123 Garden Street, Ward 5",
  "warehouseAddress": "123 Garden Street, Ward 5",
  "provinceId": 201,
  "districtId": 1442,
  "wardCode": "20101",
  "identityCardNumber": "079090123456",
  "idCardFrontUrl": "https://cdn.example.com/kyc/front.jpg",
  "idCardBackUrl": "https://cdn.example.com/kyc/back.jpg",
  "businessLicenseUrl": "https://cdn.example.com/kyc/license.pdf",
  "ownerFullName": "Nguyen Van A",
  "ownerEmail": "nguyenvana@example.com",
  "ownerPhoneNumber": "0901234567",
  "ownerGender": "Male",
  "ownerDateOfBirth": "1990-05-15T00:00:00Z",
  "ownerAddress": "123 Le Loi, Quan 1, TP.HCM",
  "bankInfo": {
    "bankName": "Vietcombank",
    "bankAccountNumber": "0123456789",
    "bankAccountHolder": "NGUYEN VAN A",
    "bankBranch": "Chi nhanh TP.HCM"
  },
  "settlementInfo": {
    "settlementCycle": "Weekly",
    "autoPayoutEnabled": true,
    "payoutStatus": "Active"
  },
  "ratingAvg": 4.8,
  "reputationScore": 98,
  "trustLevel": "High",
  "totalPositiveReviews": 150,
  "totalNegativeReviews": 2,
  "penaltyCount": 0,
  "status": "Active",
  "rejectionReason": null,
  "reviewedBy": "admin-id-123",
  "reviewedByName": "Super Admin",
  "reviewedAt": "2026-11-01T10:00:00Z",
  "createdAt": "2026-11-01T10:00:00Z",
  "updatedAt": "2026-11-01T10:00:00Z"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Target seller ID is required.` | Giá trị UUID không đúng định dạng |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Thiếu hoặc JWT token hết hạn |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Caller không có vai trò Admin hoặc Staff |
| `404 Not Found` | `SELLER_NOT_FOUND` | `Seller profile was not found.` | Không tìm thấy hồ sơ người bán theo ID |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống cơ sở dữ liệu không mong muốn |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `SellerProfileStatus.PendingVerification` | `0` | `"PendingVerification"` | Hồ sơ chờ xác minh email/OTP |
| `SellerProfileStatus.Active` | `1` | `"Active"` | Gian hàng đang hoạt động bình thường |
| `SellerProfileStatus.Rejected` | `2` | `"Rejected"` | Hồ sơ bị từ chối phê duyệt |
| `SellerProfileStatus.Suspended` | `3` | `"Suspended"` | Gian hàng đang bị tạm đình chỉ |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Trích xuất thông tin người dùng từ JWT qua `HttpContext.GetUserId()` và kiểm tra vai trò `admin`/`staff`.
2. **Bước 2:** Truy vấn `SellerProfiles` kèm thông tin liên kết từ `Users` theo `sellerId` (hoặc `userId`).
3. **Bước 3:** Ném ngoại lệ `ResourceNotFoundException("SELLER_NOT_FOUND", "Seller profile was not found.")` nếu không tồn tại.
4. **Bước 4:** Ánh xạ dữ liệu sang `AdminSellerProfileResponseDto` đảm bảo che các dữ liệu bảo mật nếu cần.
5. **Bước 5:** Trả về kết quả HTTP 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/A_Active/User/AdminSellers/GET_admin_sellers_sellerId.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Seller/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
