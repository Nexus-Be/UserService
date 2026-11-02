# USER SERVICE - PATCH /api/v1/sellers/profile

> **Feature / Use Case:** Seller — UpdateSellerOwnProfile
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Updates own seller profile and warehouse address.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PATCH`
- **Route Endpoint:** `/api/v1/sellers/profile`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** currentUserId
- **Controller:** `User.Api/Controllers/Public/SellerController.cs`
- **Application Contract:** `User.Application/Services/Seller/ISellerService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Seller/SellerDtos.cs`
- **Tên C# Record:** `UpdateSellerOwnProfileDto`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed class UpdateSellerOwnProfileDto
{
    public string? ShopName { get; set; }
    public string? Hotline { get; set; }
    public string? AvatarUrl { get; set; }
    public string? BusinessEmail { get; set; }
    public string? PickupAddress { get; set; }
    public string? StreetAddress { get; set; }
    public int? ProvinceId { get; set; }
    public int? DistrictId { get; set; }
    public string? WardCode { get; set; }
    public string? Description { get; set; }
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "shopName": "TechZone Store Official",
  "hotline": "19009999",
  "description": "Premium electronics and accessories",
  "pickupAddress": "456 Nguyen Van Linh, District 7"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Seller/SellerDtos.cs`
- **Tên C# Record:** `SellerOwnProfileUpdatedResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record SellerOwnProfileUpdatedResponseDto(
    string ShopId,
    string SellerId,
    string ShopName,
    DateTimeOffset UpdatedAt,
    string Message
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "shopId": "d4e5f6a7-b8c9-0d1e-2f3a-4b5c6d7e8f9a",
  "sellerId": "c3d4e5f6-a7b8-9c0d-1e2f-3a4b5c6d7e8f",
  "shopName": "TechZone Store Official",
  "updatedAt": "2026-11-01T11:00:00Z",
  "message": "Seller profile updated successfully."
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `One or more fields are invalid.` | Invalid format for phone or email |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Missing bearer token |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Missing seller or admin role |
| `404 Not Found` | `SELLER_NOT_FOUND` | `Seller profile was not found for the current user.` | Seller profile does not exist |
| `409 Conflict` | `SHOP_NAME_TAKEN` | `Shop name is already taken.` | Updated shop name is already used by another seller |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract currentUserId from token.
2. **Bước 2:** Locate SellerProfile for currentUserId; verify user is not banned.
3. **Bước 3:** Check shopName uniqueness if changed.
4. **Bước 4:** Update profile fields and log audit record.
5. **Bước 5:** Persist changes in database.
6. **Bước 6:** Return SellerOwnProfileUpdatedResponseDto with 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Seller/PATCH_sellers_profile.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Seller/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
