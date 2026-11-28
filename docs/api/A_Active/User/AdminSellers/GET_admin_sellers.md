# [GET] /api/v1/admin/sellers

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Seller`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Lấy danh sách hồ sơ người bán (Seller) toàn hệ thống có phân trang, hỗ trợ lọc theo trạng thái gian hàng (`status`), từ khóa tìm kiếm (`keyword`) và trạng thái sẵn sàng kho hàng GHN (`warehouseReady`).
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/admin/sellers`
* **Quyền hạn truy cập (RBAC):** `Admin` hoặc `Staff` (`[RequireRole("admin", "staff")]`)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/AdminSellersController.cs` $\rightarrow$ `ListAdminSellers()`
* **Application Interface:** `User.Application/Services/Seller/ISellerService.cs` $\rightarrow$ `GetAdminSellersAsync()`
* **Service Implementation:** `User.Application/Services/Seller/SellerService.cs` $\rightarrow$ `GetAdminSellersAsync()`
* **Item DTO:** `User.Application/Services/Seller/SellerDtos.cs` $\rightarrow$ `AdminSellerListItemDto`
* **Thực thể & Bảng DB:** `User.Domain/Entities/SellerProfile.cs` $\rightarrow$ Bảng `SellerProfiles` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Query Parameters
| Tên tham số | Kiểu dữ liệu | Mặc định | Ràng buộc & Khoảng giá trị |
|:---|:---|:---:|:---|
| `page` | `integer` | `1` | Số trang (tối thiểu 1). |
| `pageSize` | `integer` | `20` | Kích thước trang ($1 \le pageSize \le 100$). |
| `status` | `SellerProfileStatus?` | `null` | Lọc theo trạng thái (`Approved`, `Suspended`, ...). |
| `keyword` | `string?` | `null` | Tìm theo tên shop hoặc hotline. |
| `warehouseReady` | `bool?` | `null` | Lọc các shop đã/chưa tạo xong kho hàng GHN (`false` để tìm shop cần retry tạo kho). |

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
      "status": "Approved",
      "hotline": "0901234567",
      "provinceId": 202,
      "districtId": 1442,
      "createdAt": "2026-11-01T10:00:00Z",
      "warehouseReady": true
    }
  ],
  "pagination": {
    "currentPage": 1,
    "pageSize": 20,
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
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Thiếu hoặc token không hợp lệ. |
| `403` | `FORBIDDEN` | "You do not have permission to access this resource." | Người dùng không có vai trò `admin` hoặc `staff`. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `ISellerService.cs` và `SellerService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `AdminSellersController.cs` (`GET /api/v1/admin/sellers`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test User.sln -c Release` vượt qua 100%.
