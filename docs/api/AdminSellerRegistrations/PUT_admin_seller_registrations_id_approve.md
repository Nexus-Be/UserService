# USER SERVICE - PUT /api/v1/admin/seller-registrations/{id}/approve

> **Feature / Use Case:** AdminSellerRegistrations — ApproveSellerRegistration
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Vinh & AI Agent
> **Mô tả chức năng:** Approves a seller registration, provisions fulfillment warehouse, and assigns Seller role.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PUT`
- **Route Endpoint:** `/api/v1/admin/seller-registrations/{id}/approve`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin", "staff")`
- **Quy tắc Sở hữu (Resource Ownership):** Admin/Staff authority
- **Controller:** `User.Api/Controllers/Public/AdminSellerRegistrationsController.cs`
- **Application Contract:** `User.Application/Services/Seller/ISellerService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Target registration ID |

### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Seller/SellerRequests.cs`
- **Tên C# Record:** `ApproveSellerRegistrationDto`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed class ApproveSellerRegistrationDto
{
    public string? Note { get; set; }
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "note": "KYC documentation verified successfully."
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Seller/SellerResponses.cs`
- **Tên C# Record:** `ApproveSellerRegistrationResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record ApproveSellerRegistrationResponseDto(
    string RegistrationId,
    string UserId,
    string Status,
    DateTimeOffset ApprovedAt
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "registrationId": "f7a8b9c0-d1e2-3a4b-5c6d-7e8f9a0b1c2d",
  "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "status": "Approved",
  "approvedAt": "2026-11-01T14:30:00Z"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Registration identifier is required.` | Invalid registration GUID |
| `400 Bad Request` | `INVALID_STATUS` | `Only pending seller registrations can be approved.` | Registration is not in Pending status |
| `400 Bad Request` | `USER_NOT_ACTIVE` | `User account must be active to approve seller registration.` | User is banned or inactive |
| `400 Bad Request` | `REPUTATION_TOO_LOW` | `User reputation score is below the required threshold (50).` | User reputationScore < 50 |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Missing bearer token |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Caller is not Admin or Staff |
| `404 Not Found` | `REGISTRATION_NOT_FOUND` | `Seller registration not found.` | Registration ID does not exist |
| `409 Conflict` | `ALREADY_APPROVED` | `Seller registration has already been approved.` | Registration already Approved |
| `502 Bad Gateway` | `FULFILLMENT_UNAVAILABLE` | `Fulfillment warehouse creation failed or timed out. Please try again.` | Downstream warehouse provisioning failure |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `SellerProfileStatus.Approved` | `1` | "Approved" | Approved seller status |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract reviewerId (Admin/Staff) from token.
2. **Bước 2:** Locate registration record in SellerRegistrations; verify Status == Pending.
3. **Bước 3:** Verify associated User is active and reputation score >= 50.
4. **Bước 4:** Create new SellerProfile and assign Seller role to user.
5. **Bước 5:** Set Registration.Status = Approved, ReviewedBy = reviewerId, ReviewedAt = UtcNow.
6. **Bước 6:** Record audit log via _dbContext.AddAuditLog(...).
7. **Bước 7:** Return ApproveSellerRegistrationResponseDto with 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/AdminSellerRegistrations/PUT_admin_seller_registrations_id_approve.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/AdminSellerRegistrations/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
