# [GET] /api/v1/sellers

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Seller`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Tìm kiếm danh sách các gian hàng (Shop) đang hoạt động công khai theo tên shop (phân trang).
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/sellers`
* **Quyền hạn truy cập (RBAC):** `AllowAnonymous` (Công khai)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/SellerController.cs` $\rightarrow$ `SearchPublicSellers()`
* **Application Interface:** `User.Application/Services/Seller/ISellerService.cs` $\rightarrow$ `SearchPublicSellersAsync()`
* **Service Implementation:** `User.Application/Services/Seller/SellerService.cs` $\rightarrow$ `SearchPublicSellersAsync()`
* **Item DTO:** `User.Application/Services/Seller/SellerResponses.cs` $\rightarrow$ `PublicSellerProfileResponseDto`
* **Thực thể & Bảng DB:** `User.Domain/Entities/SellerProfile.cs` $\rightarrow$ Bảng `SellerProfiles` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Query Parameters
| Tên tham số | Kiểu dữ liệu | Mặc định | Ràng buộc & Khoảng giá trị |
|:---|:---|:---:|:---|
| `search` | `string?` | `null` | Từ khóa tìm kiếm theo tên shop. |
| `page` | `integer` | `1` | Số trang (tối thiểu 1). |
| `pageSize` | `integer` | `12` | Kích thước trang ($1 \le pageSize \le 100$). |

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "items": [
    {
      "sellerId": "22222222-aaaa-bbbb-cccc-000000000001",
      "userId": "cccccccc-3333-3333-3333-333333333333",
      "shopName": "Nexus Audio Store",
      "avatarUrl": "https://res.cloudinary.com/.../shop.png",
      "description": "Authentic audio equipment.",
      "hotline": "0901234567",
      "provinceName": "Ho Chi Minh",
      "status": "Approved",
      "sellerReputationScore": 55,
      "createdAt": "2026-11-01T10:00:00Z"
    }
  ],
  "pagination": {
    "currentPage": 1,
    "pageSize": 12,
    "totalCount": 1,
    "totalPages": 1,
    "hasPrevious": false,
    "hasNext": false
  }
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `400` | `VAL_INVALID_INPUT` | "One or more validation errors occurred." | Tham số phân trang không hợp lệ. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `ISellerService.cs` và `SellerService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `SellerController.cs` (`GET /api/v1/sellers`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test User.sln -c Release` vượt qua 100%.
