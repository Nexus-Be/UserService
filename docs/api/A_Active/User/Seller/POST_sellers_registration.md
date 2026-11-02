# USER SERVICE - POST /api/v1/sellers/registration

> **Feature / Use Case:** Seller — RegisterSeller
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Submits a seller onboarding registration application and triggers business email verification OTP.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/sellers/registration`
- **Quyền hạn (RBAC Policy):** `RequireRole (Authenticated)`
- **Quy tắc Sở hữu (Resource Ownership):** currentUserId (from HttpContext claims)
- **Controller:** `User.Api/Controllers/Public/SellerController.cs`
- **Application Contract:** `User.Application/Services/Seller/ISellerService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Seller/SellerRequests.cs`
- **Tên C# Record:** `CreateSellerRegistrationDto`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed class CreateSellerRegistrationDto
{
    [Required(ErrorMessage = "Shop name is required.")]
    public string? ShopName { get; set; }

    [Required(ErrorMessage = "Business email is required.")]
    [EmailAddress(ErrorMessage = "Invalid business email format.")]
    public string? BusinessEmail { get; set; }

    [Required(ErrorMessage = "Hotline is required.")]
    public string? Hotline { get; set; }

    [Required(ErrorMessage = "Identity card number is required.")]
    public string? IdentityCardNumber { get; set; }

    public string? IdCardFrontUrl { get; set; }
    public string? IdCardBackUrl { get; set; }
    public string? BusinessLicenseUrl { get; set; }
    public string? AvatarUrl { get; set; }

    public WarehouseAddressDto? WarehouseAddress { get; set; }
    public string? PickupAddress { get; set; }
    public string? StreetAddress { get; set; }
    public int? ProvinceId { get; set; }
    public int? DistrictId { get; set; }
    public string? WardCode { get; set; }
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "shopName": "TechZone Store",
  "businessEmail": "contact@techzone.vn",
  "hotline": "19001234",
  "identityCardNumber": "012345678901",
  "idCardFrontUrl": "https://cdn.example.com/kyc/front.jpg",
  "idCardBackUrl": "https://cdn.example.com/kyc/back.jpg",
  "businessLicenseUrl": "https://cdn.example.com/kyc/license.pdf",
  "warehouseAddress": {
    "provinceId": 201,
    "districtId": 1442,
    "wardCode": "20101",
    "streetAddress": "123 Nguyen Van Linh, District 7"
  }
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Seller/SellerResponses.cs`
- **Tên C# Record:** `SellerRegistrationResponseDto`
- **Mã HTTP thành công:** `201 Created`
- **Khai báo C# Record:**
```csharp
public sealed record SellerRegistrationResponseDto(
    string BusinessEmail,
    string Status,
    int OtpExpiresInSeconds,
    string NextStep,
    DateTimeOffset SubmittedAt
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "businessEmail": "contact@techzone.vn",
  "status": "PendingVerification",
  "otpExpiresInSeconds": 300,
  "nextStep": "VerifyBusinessEmailOtp",
  "submittedAt": "2026-11-01T10:30:00Z"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `One or more seller registration fields are invalid.` | Validation failed on registration payload |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Missing or expired token |
| `409 Conflict` | `SELLER_ALREADY_EXISTS` | `User is already registered as an active seller.` | User already owns an approved seller profile |
| `409 Conflict` | `PENDING_REGISTRATION_EXISTS` | `A pending seller registration application already exists.` | User already has a pending onboarding draft |
| `409 Conflict` | `SHOP_NAME_TAKEN` | `Shop name is already taken.` | Another merchant is using this shop name |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `SellerProfileStatus.Pending` | `0` | "Pending" | Pending admin review |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract currentUserId from token claims.
2. **Bước 2:** Check user active status and verify user does not already have an active seller profile.
3. **Bước 3:** Verify shopName is unique across registered shops.
4. **Bước 4:** Persist seller registration draft in Redis session.
5. **Bước 5:** Generate OTP code and send to BusinessEmail via IEmailNotificationClient.
6. **Bước 6:** Return 201 Created with SellerRegistrationResponseDto.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Seller/POST_sellers_registration.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Seller/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
