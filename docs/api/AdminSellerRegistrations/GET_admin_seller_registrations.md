# USER SERVICE - GET /api/v1/admin/seller-registrations

> **Feature / Use Case:** AdminSellerRegistrations — ListSellerRegistrations
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** AI Assistant (UserService Architecture Team)
> **Mô tả chức năng:** Lists pending seller registration applications for review with pagination.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/admin/seller-registrations`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin", "staff")`
- **Quy tắc Sở hữu (Resource Ownership):** Admin/Staff authority
- **Controller:** `User.Api/Controllers/Public/AdminSellerRegistrationsController.cs`
- **Application Contract:** `User.Application/Services/Seller/ISellerService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.2 Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `page` | Query | `int` | Không | Page number (Default: 1) | Tham số truy vấn |
| `pageSize` | Query | `int` | Không | Items per page (Default: 20) | Tham số truy vấn |
| `status` | Query | `string` | Không | Filter by status (Pending, Approved, Rejected) | Tham số truy vấn |

### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Api/Common/PageQuery.cs`
- **Tên C# Record:** `PageQuery`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed record PageQuery(int Page = 1, int PageSize = 20);
```
- **JSON Request Body mẫu (camelCase):**
```json
{}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Common/Models/PagedData.cs`
- **Tên C# Record:** `PagedData<AdminSellerRegistrationItemDto>`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record AdminSellerRegistrationItemDto(
    string RegistrationId,
    string UserId,
    string ShopName,
    string? BusinessEmail,
    string? Hotline,
    string? IdentityCardNumber,
    string? WarehouseAddress,
    string? PickupAddress,
    int ReputationScore,
    string Status,
    DateTimeOffset CreatedAt
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "items": [
    {
      "registrationId": "f7a8b9c0-d1e2-3a4b-5c6d-7e8f9a0b1c2d",
      "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
      "shopName": "TechZone Store",
      "businessEmail": "contact@techzone.vn",
      "hotline": "19001234",
      "identityCardNumber": "012345678901",
      "warehouseAddress": "123 Nguyen Van Linh, District 7",
      "pickupAddress": "123 Nguyen Van Linh, District 7",
      "reputationScore": 100,
      "status": "Pending",
      "createdAt": "2026-11-01T10:35:00Z"
    }
  ],
  "totalItems": 12,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1,
  "hasNext": false,
  "hasPrevious": false
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Missing bearer token |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Caller is not Admin or Staff |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `SellerProfileStatus.Pending` | `0` | "Pending" | Under review |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract pagination and optional status filter.
2. **Bước 2:** Query SellerRegistrations with database pagination (Skip/Take).
3. **Bước 3:** Return PagedData<AdminSellerRegistrationItemDto>.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/AdminSellerRegistrations/GET_admin_seller_registrations.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/AdminSellerRegistrations/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
