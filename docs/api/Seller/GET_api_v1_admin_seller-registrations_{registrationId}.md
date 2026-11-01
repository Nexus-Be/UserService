# USER SERVICE - GET /api/v1/admin/seller-registrations/{registrationId}

> **Feature / Use Case:** ADMIN-07B (GetSellerRequestDetails)
> **Trạng thái:** ✅ VERIFIED (Build 0 errors, 268/268 tests passed)
> **Ngày tạo:** 2026-11-01
> **Tác giả:** AI Assistant

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/admin/seller-registrations/{registrationId}`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin", "staff")`
- **Quy tắc Sở hữu (Resource Ownership):** Không áp dụng kiểm tra sở hữu cá nhân; Admin và Staff có quyền xem chi tiết toàn bộ đơn đăng ký seller.
- **Controller:** `User.Api/Controllers/Public/AdminSellerRegistrationsController.cs`
- **Application Contract:** `User.Application/Services/Seller/ISellerService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `registrationId` | Route | `Guid` | Có | UUIDv4 format (`{registrationId:guid}`) | Định danh đơn đăng ký người bán (`SellerProfile.Id` hoặc `UserId`) |

### 2.2 Request Body
- Endpoint sử dụng phương thức `GET`, không có Request Body.

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần, KHÔNG bọc qua `ApiResponse<T>` hay `ResultFilter`. API Gateway sẽ chịu trách nhiệm đóng gói phong bì ở tầng biên nếu cần.

- **Vị trí file:** `User.Application/Services/Seller/SellerResponses.cs`
- **Tên C# Record:** `SellerRegistrationDetailsDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record SellerRegistrationDetailsDto(
    [property: JsonPropertyName("registrationId")] string RegistrationId,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("applicantName")] string ApplicantName,
    [property: JsonPropertyName("applicantEmail")] string ApplicantEmail,
    [property: JsonPropertyName("applicantPhone")] string? ApplicantPhone,
    [property: JsonPropertyName("shopName")] string ShopName,
    [property: JsonPropertyName("avatarUrl")] string? AvatarUrl,
    [property: JsonPropertyName("businessEmail")] string? BusinessEmail,
    [property: JsonPropertyName("hotline")] string Hotline,
    [property: JsonPropertyName("identityCardNumber")] string? IdentityCardNumber,
    [property: JsonPropertyName("idCardFrontUrl")] string? IdCardFrontUrl,
    [property: JsonPropertyName("idCardBackUrl")] string? IdCardBackUrl,
    [property: JsonPropertyName("businessLicenseUrl")] string? BusinessLicenseUrl,
    [property: JsonPropertyName("pickupAddress")] string PickupAddress,
    [property: JsonPropertyName("warehouseAddress")] string WarehouseAddress,
    [property: JsonPropertyName("provinceId")] int ProvinceId,
    [property: JsonPropertyName("districtId")] int DistrictId,
    [property: JsonPropertyName("wardCode")] string WardCode,
    [property: JsonPropertyName("reputationScore")] int ReputationScore,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("rejectionReason")] string? RejectionReason,
    [property: JsonPropertyName("reviewedBy")] string? ReviewedBy,
    [property: JsonPropertyName("reviewedAt")] DateTimeOffset? ReviewedAt,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset? UpdatedAt
);
```

- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "registrationId": "4a7f23be-9311-471e-ba5e-8219cb4f8d22",
  "userId": "1b9d6bcd-bbfd-4b2d-9b5d-ab8dfbbd4bed",
  "applicantName": "Nguyen Van A",
  "applicantEmail": "applicant@example.com",
  "applicantPhone": "0912345678",
  "shopName": "Shop Cong Nghe",
  "avatarUrl": "https://cdn.example.com/avatars/shop.jpg",
  "businessEmail": "contact@shopcongnghe.com",
  "hotline": "0987654321",
  "identityCardNumber": "001200000001",
  "idCardFrontUrl": "https://cdn.example.com/cccd_front.jpg",
  "idCardBackUrl": "https://cdn.example.com/cccd_back.jpg",
  "businessLicenseUrl": "https://cdn.example.com/business_license.pdf",
  "pickupAddress": "123 Duong Le Loi, Phuong Ben Nghe, Quan 1, TP Ho Chi Minh",
  "warehouseAddress": "123 Duong Le Loi, Phuong Ben Nghe, Quan 1, TP Ho Chi Minh",
  "provinceId": 202,
  "districtId": 1442,
  "wardCode": "20101",
  "reputationScore": 50,
  "status": "Pending",
  "rejectionReason": null,
  "reviewedBy": null,
  "reviewedAt": null,
  "createdAt": "2026-11-01T08:00:00Z",
  "updatedAt": null
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Registration identifier is required.` | `registrationId == Guid.Empty` |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Thiếu Header Authorization hoặc JWT không hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `Forbidden: Missing required role(s): admin, staff` | Token không chứa vai trò `admin` hoặc `staff` |
| `404 Not Found` | `SELLER_REGISTRATION_NOT_FOUND` | `Seller registration application not found.` | Không tìm thấy bản ghi `SellerProfile` theo `registrationId` |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Ngoại lệ hệ thống |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1 (Api Boundary):** `AdminSellerRegistrationsController.GetSellerRequestDetails(Guid registrationId)` nhận request, bắt buộc vai trò `admin` hoặc `staff`.
2. **Bước 2 (Validation):** Kiểm tra `registrationId != Guid.Empty`, ném `VAL_INVALID_INPUT` nếu rỗng.
3. **Bước 3 (Query):** Truy vấn `SellerProfiles` bằng `AsNoTracking()`, nạp kèm `User` và `User.ReputationScore`. Tìm kiếm theo `Id == registrationId` (fallback theo `UserId == registrationId`).
4. **Bước 4 (Not Found Guard):** Ném `DomainException("SELLER_REGISTRATION_NOT_FOUND", "Seller registration application not found.", 404)` nếu không có dữ liệu.
5. **Bước 5 (Mapping):** Ánh xạ sang `SellerRegistrationDetailsDto` với các thông tin chứng từ (`IdCardFrontUrl`, `IdCardBackUrl`, `BusinessLicenseUrl`), kho GHN (`ProvinceId`, `DistrictId`, `WardCode`, `PickupAddress`) và thông tin người nộp đơn.
6. **Bước 6 (Response):** Trả về HTTP 200 OK kèm DTO.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Seller/GET_api_v1_admin_seller-registrations_{registrationId}.md`.
- [x] **Application:** Thêm `SellerRegistrationDetailsDto` vào `SellerResponses.cs`.
- [x] **Application:** Thêm `GetSellerRegistrationDetailsAsync` vào `ISellerService.cs` và `SellerService.cs`.
- [x] **Api:** Thêm action `GetSellerRequestDetails` vào `AdminSellerRegistrationsController.cs`.
- [x] **Tests:** Viết unit tests bao phủ trong `SellerControllerRestTests.cs` và `SellerServiceTests.cs`.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
