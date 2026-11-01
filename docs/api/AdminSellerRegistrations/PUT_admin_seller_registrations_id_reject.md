# USER SERVICE - PUT /api/v1/admin/seller-registrations/{id}/reject

> **Feature / Use Case:** AdminSellerRegistrations — RejectSellerRegistration
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** AI Assistant (UserService Architecture Team)
> **Mô tả chức năng:** Rejects a seller registration application with a required reason.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PUT`
- **Route Endpoint:** `/api/v1/admin/seller-registrations/{id}/reject`
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
- **Tên C# Record:** `RejectSellerRegistrationDto`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed class RejectSellerRegistrationDto
{
    [Required(ErrorMessage = "Rejection reason is required.")]
    public string? RejectionReason { get; set; }
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "rejectionReason": "ID card photo is blurred and unreadable. Please submit clear scanned copy."
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Seller/SellerResponses.cs`
- **Tên C# Record:** `RejectSellerRegistrationResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record RejectSellerRegistrationResponseDto(
    string RegistrationId,
    string UserId,
    string Status,
    DateTimeOffset RejectedAt,
    string RejectionReason
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "registrationId": "f7a8b9c0-d1e2-3a4b-5c6d-7e8f9a0b1c2d",
  "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "status": "Rejected",
  "rejectedAt": "2026-11-01T14:45:00Z",
  "rejectionReason": "ID card photo is blurred and unreadable. Please submit clear scanned copy."
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Rejection reason is required.` | Empty rejection reason |
| `400 Bad Request` | `INVALID_STATUS` | `Approved seller registrations cannot be rejected.` | Registration is already in Approved status |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Missing bearer token |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Caller is not Admin or Staff |
| `404 Not Found` | `REGISTRATION_NOT_FOUND` | `Seller registration not found.` | Registration ID does not exist |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `SellerProfileStatus.Rejected` | `2` | "Rejected" | Rejected seller registration |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract reviewerId (Admin/Staff) from token.
2. **Bước 2:** Locate registration in SellerRegistrations; verify not already Approved.
3. **Bước 3:** Set Status = Rejected, RejectionReason = dto.RejectionReason, ReviewedBy = reviewerId, ReviewedAt = UtcNow.
4. **Bước 4:** Record audit log.
5. **Bước 5:** Return RejectSellerRegistrationResponseDto with 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/AdminSellerRegistrations/PUT_admin_seller_registrations_id_reject.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/AdminSellerRegistrations/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
