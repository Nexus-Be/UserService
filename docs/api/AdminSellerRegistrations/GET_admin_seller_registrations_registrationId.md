# USER SERVICE - GET /api/v1/admin/seller-registrations/{registrationId}

> **Feature / Use Case:** AdminSellerRegistrations — GetSellerRequestDetails
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Vinh & AI Agent
> **Mô tả chức năng:** Retrieves full details of a seller registration application for administrative review.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/admin/seller-registrations/{registrationId}`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin", "staff")`
- **Quy tắc Sở hữu (Resource Ownership):** Admin/Staff authority
- **Controller:** `User.Api/Controllers/Public/AdminSellerRegistrationsController.cs`
- **Application Contract:** `User.Application/Services/Seller/ISellerService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `registrationId` | Route | `Guid` | Có | UUIDv4 format | Target registration application UUID |

### 2.3 Request Body Record DTO
- **Vị trí file:** `None (Route Parameter)`
- **Tên C# Record:** `None`
- **Khai báo C# & DataAnnotations:**
```csharp
// Route parameter: Guid registrationId
```
- **JSON Request Body mẫu (camelCase):**
```json
{}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Seller/SellerResponses.cs`
- **Tên C# Record:** `SellerRegistrationDetailsDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record SellerRegistrationDetailsDto(
    string RegistrationId,
    string UserId,
    string ApplicantName,
    string ApplicantEmail,
    string? ApplicantPhone,
    string ShopName,
    string? AvatarUrl,
    string? BusinessEmail,
    string Hotline,
    string? IdentityCardNumber,
    string? IdCardFrontUrl,
    string? IdCardBackUrl,
    string? BusinessLicenseUrl,
    string PickupAddress,
    string WarehouseAddress,
    int ProvinceId,
    int DistrictId,
    string WardCode,
    int ReputationScore,
    string Status,
    string? RejectionReason,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "registrationId": "f7a8b9c0-d1e2-3a4b-5c6d-7e8f9a0b1c2d",
  "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "applicantName": "John Doe",
  "applicantEmail": "buyer@example.com",
  "applicantPhone": "0987654321",
  "shopName": "TechZone Store",
  "avatarUrl": "https://cdn.example.com/shops/techzone.png",
  "businessEmail": "contact@techzone.vn",
  "hotline": "19001234",
  "identityCardNumber": "012345678901",
  "idCardFrontUrl": "https://cdn.example.com/kyc/front.jpg",
  "idCardBackUrl": "https://cdn.example.com/kyc/back.jpg",
  "businessLicenseUrl": "https://cdn.example.com/kyc/license.pdf",
  "pickupAddress": "123 Nguyen Van Linh, District 7",
  "warehouseAddress": "123 Nguyen Van Linh, District 7",
  "provinceId": 201,
  "districtId": 1442,
  "wardCode": "20101",
  "reputationScore": 100,
  "status": "Pending",
  "rejectionReason": null,
  "reviewedBy": null,
  "reviewedAt": null,
  "createdAt": "2026-11-01T10:35:00Z",
  "updatedAt": null
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Registration identifier is required.` | Invalid GUID format |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Missing bearer token |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Caller is not Admin or Staff |
| `404 Not Found` | `REGISTRATION_NOT_FOUND` | `Seller registration not found.` | Registration application does not exist |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `SellerProfileStatus.Pending` | `0` | "Pending" | Under review |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Validate registrationId GUID.
2. **Bước 2:** Query SellerRegistration application joining User for applicant info.
3. **Bước 3:** Return SellerRegistrationDetailsDto with 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/AdminSellerRegistrations/GET_admin_seller_registrations_registrationId.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/AdminSellerRegistrations/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
