# USER SERVICE - POST /api/v1/sellers/otp/resend

> **Feature / Use Case:** Seller — ResendSellerOtp
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Vinh & AI Agent
> **Mô tả chức năng:** Resends OTP verification code for pending seller registration draft.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/sellers/otp/resend`
- **Quyền hạn (RBAC Policy):** `RequireRole (Authenticated)`
- **Quy tắc Sở hữu (Resource Ownership):** currentUserId
- **Controller:** `User.Api/Controllers/Public/SellerController.cs`
- **Application Contract:** `User.Application/Services/Seller/ISellerService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Seller/SellerRequests.cs`
- **Tên C# Record:** `ResendSellerOtpDto`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed class ResendSellerOtpDto
{
    [Required(ErrorMessage = "Business email is required.")]
    public string BusinessEmail { get; set; } = string.Empty;
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "businessEmail": "contact@techzone.vn"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `Pure JSON Object`
- **Tên C# Record:** `{ message: string }`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
// Returns anonymous object: new { message = "Verification code resent successfully." }
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "message": "Verification code resent successfully."
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Business email is required.` | Empty business email |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Missing bearer token |
| `404 Not Found` | `DRAFT_NOT_FOUND` | `Registration session expired or does not exist. Please submit registration again.` | No active seller registration draft in Redis |
| `429 Too Many Requests` | `OTP_COOLDOWN_ACTIVE` | `Please wait 60 seconds before requesting a new OTP.` | Cooldown key active in Redis |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract currentUserId.
2. **Bước 2:** Verify active registration draft in Redis.
3. **Bước 3:** Check 60-second cooldown in Redis.
4. **Bước 4:** Generate new OTP, update Redis TTL, and send email via IEmailNotificationClient.
5. **Bước 5:** Return 200 OK with success message.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Seller/POST_sellers_otp_resend.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Seller/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
