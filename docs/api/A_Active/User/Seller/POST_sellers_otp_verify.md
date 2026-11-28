# USER SERVICE - POST /api/v1/sellers/otp/verify

> **Feature / Use Case:** Seller — VerifySellerOtp
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Verifies seller registration OTP code and moves application into Pending review state.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/sellers/otp/verify`
- **Quyền hạn (RBAC Policy):** `RequireRole (Authenticated)`
- **Quy tắc Sở hữu (Resource Ownership):** currentUserId
- **Controller:** `User.Api/Controllers/Public/SellerController.cs`
- **Application Contract:** `User.Application/Services/Seller/ISellerService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Seller/SellerRequests.cs`
- **Tên C# Record:** `VerifySellerOtpDto`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed class VerifySellerOtpDto
{
    [Required(ErrorMessage = "Business email is required.")]
    public string BusinessEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "OTP code is required.")]
    public string OtpCode { get; set; } = string.Empty;
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "businessEmail": "contact@techzone.vn",
  "otpCode": "123456"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Seller/SellerResponses.cs`
- **Tên C# Record:** `VerifySellerOtpResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record VerifySellerOtpResponseDto(
    string SellerId,
    string UserId,
    string Status,
    string BusinessEmail,
    string ShopName,
    DateTimeOffset VerifiedAt,
    string Message
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "sellerId": "c3d4e5f6-a7b8-9c0d-1e2f-3a4b5c6d7e8f",
  "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "status": "Pending",
  "businessEmail": "contact@techzone.vn",
  "shopName": "TechZone Store",
  "verifiedAt": "2026-11-01T10:35:00Z",
  "message": "Business email verified successfully. Application is pending review."
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Business email and OTP code are required.` | Empty email or OTP |
| `400 Bad Request` | `OTP_INVALID` | `Invalid OTP code.` | OTP does not match Redis cache |
| `400 Bad Request` | `OTP_EXPIRED` | `OTP has expired.` | OTP has expired in Redis |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Missing bearer token |
| `404 Not Found` | `DRAFT_NOT_FOUND` | `Registration session expired or does not exist. Please submit registration again.` | Draft session in Redis expired |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `SellerProfileStatus.Pending` | `0` | "Pending" | Application awaiting admin review |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract currentUserId from token.
2. **Bước 2:** Retrieve registration draft from Redis; throw DRAFT_NOT_FOUND if missing.
3. **Bước 3:** Verify OTP code against cached Redis key.
4. **Bước 4:** Persist SellerRegistration application in PostgreSQL with Status = Pending.
5. **Bước 5:** Clean up Redis draft and OTP keys.
6. **Bước 6:** Record audit log for seller registration submission.
7. **Bước 7:** Return VerifySellerOtpResponseDto with 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Seller/POST_sellers_otp_verify.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Seller/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
