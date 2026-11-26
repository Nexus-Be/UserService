# [POST] /api/v1/admin/sellers/{sellerId}/warehouse/retry

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Seller`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Thử tạo lại kho lấy hàng GHN bên FulfillmentService cho một Seller đã được duyệt (`Approved`) nhưng trước đó bị lỗi tạo kho tại thời điểm duyệt đơn. An toàn khi gọi lặp lại (idempotent).
* **HTTP Method:** `POST`
* **Đường dẫn chuẩn:** `/api/v1/admin/sellers/{sellerId}/warehouse/retry`
* **Quyền hạn truy cập (RBAC):** `Admin` hoặc `Staff` (`[RequireRole("admin", "staff")]`)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/AdminSellersController.cs` $\rightarrow$ `RetryWarehouse()`
* **Application Interface:** `User.Application/Services/Seller/ISellerService.cs` $\rightarrow$ `RetryWarehouseAsync()`
* **Service Implementation:** `User.Application/Services/Seller/SellerService.cs` $\rightarrow$ `RetryWarehouseAsync()`
* **Outbound Client:** `IFulfillmentClient` $\rightarrow$ `POST /api/v1/internal/warehouses`
* **Thực thể & Bảng DB:** `User.Domain/Entities/SellerProfile.cs` $\rightarrow$ Bảng `SellerProfiles` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters
| Tên tham số | Kiểu dữ liệu | Bắt buộc | Ràng buộc & Mô tả |
|:---|:---|:---:|:---|
| `sellerId` | `Guid` | Có | Định danh hồ sơ người bán (`SellerProfile.Id`). |

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "sellerId": "22222222-aaaa-bbbb-cccc-000000000001",
  "warehouseReady": true
}
```
* `warehouseReady`: `true` nếu đã tạo/đồng bộ kho thành công; `false` nếu FulfillmentService vẫn đang lỗi.

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Thiếu hoặc token không hợp lệ. |
| `403` | `FORBIDDEN` | "You do not have permission to access this resource." | Người dùng không có vai trò `admin` hoặc `staff`. |
| `404` | `SELLER_NOT_FOUND` | "Seller profile not found." | Không tìm thấy hồ sơ người bán theo `sellerId`. |
| `409` | `SELLER_NOT_APPROVED` | "Seller profile is not in approved status." | Hồ sơ người bán chưa ở trạng thái `Approved`. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `ISellerService.cs` và `SellerService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `AdminSellersController.cs` (`POST /api/v1/admin/sellers/{sellerId}/warehouse/retry`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test User.sln -c Release` vượt qua 100%.
