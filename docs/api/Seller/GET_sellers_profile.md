# USER SERVICE - GET /api/v1/sellers/profile

> **Feature / Use Case:** Seller — GetSellerOwnProfile
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** AI Assistant (UserService Architecture Team)
> **Mô tả chức năng:** Retrieves own seller profile for the authenticated merchant.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/sellers/profile`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** currentUserId
- **Controller:** `User.Api/Controllers/Public/SellerController.cs`
- **Application Contract:** `User.Application/Services/Seller/ISellerService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `None (Header Bearer Token)`
- **Tên C# Record:** `None`
- **Khai báo C# & DataAnnotations:**
```csharp
// Identity extracted via HttpContext.GetUserId()
```
- **JSON Request Body mẫu (camelCase):**
```json
{}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Seller/SellerDtos.cs`
- **Tên C# Record:** `SellerOwnProfileResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record SellerOwnProfileResponseDto(
    string SellerId,
    string ShopId,
    string UserId,
    string ShopName,
    string? AvatarUrl,
    string? BusinessEmail,
    string Hotline,
    string? Description,
    string PickupAddress,
    int ProvinceId,
    int DistrictId,
    string WardCode,
    string Status,
    double RatingAvg,
    int ReputationScore,
    string? TrustLevel,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "sellerId": "c3d4e5f6-a7b8-9c0d-1e2f-3a4b5c6d7e8f",
  "shopId": "d4e5f6a7-b8c9-0d1e-2f3a-4b5c6d7e8f9a",
  "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "shopName": "TechZone Store",
  "avatarUrl": "https://cdn.example.com/shops/techzone.png",
  "businessEmail": "contact@techzone.vn",
  "hotline": "19001234",
  "description": "Official electronics store",
  "pickupAddress": "123 Nguyen Van Linh, District 7",
  "provinceId": 201,
  "districtId": 1442,
  "wardCode": "20101",
  "status": "Approved",
  "ratingAvg": 4.8,
  "reputationScore": 120,
  "trustLevel": "Standard",
  "createdAt": "2026-03-01T08:00:00Z",
  "updatedAt": "2026-10-15T09:30:00Z"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Missing bearer token |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | User does not hold seller or admin role |
| `404 Not Found` | `SELLER_NOT_FOUND` | `Seller profile was not found for the current user.` | No seller profile associated with this account |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `SellerProfileStatus.Approved` | `1` | "Approved" | Approved active seller |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract currentUserId from token.
2. **Bước 2:** Query SellerProfiles table matching UserId == currentUserId.
3. **Bước 3:** Assemble SellerOwnProfileResponseDto.
4. **Bước 4:** Return 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Seller/GET_sellers_profile.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Seller/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
