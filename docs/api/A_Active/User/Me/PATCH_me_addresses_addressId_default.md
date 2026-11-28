# [PATCH] /api/v1/me/addresses/{addressId}/default

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Address`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Đặt một địa chỉ giao hàng cụ thể làm địa chỉ mặc định của người dùng hiện tại (tự động bỏ cờ mặc định của các địa chỉ khác).
* **HTTP Method:** `PATCH`
* **Đường dẫn chuẩn:** `/api/v1/me/addresses/{addressId}/default`
* **Quyền hạn truy cập (RBAC):** `Authenticated` (`[RequireRole]`) + Resource Ownership (`UserId == currentUserId`).

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/MeController.cs` $\rightarrow$ `SetMyDefaultAddress()`
* **Application Interface:** `User.Application/Services/Address/IAddressService.cs` $\rightarrow$ `SetDefaultAddressAsync()`
* **Service Implementation:** `User.Application/Services/Address/AddressService.cs` $\rightarrow$ `SetDefaultAddressAsync()`
* **Response DTO:** `User.Application/Services/Address/AddressResponses.cs` $\rightarrow$ `UserAddressDto`
* **Thực thể & Bảng DB:** `User.Domain/Entities/UserAddress.cs` $\rightarrow$ Bảng `UserAddresses` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters
| Tên tham số | Kiểu dữ liệu | Bắt buộc | Ràng buộc & Mô tả |
|:---|:---|:---:|:---|
| `addressId` | `Guid` | Có | Định danh địa chỉ cần đặt làm mặc định (`UserAddress.Id`). |

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "id": "11111111-aaaa-bbbb-cccc-000000000001",
  "userId": "dddddddd-4444-4444-4444-444444444444",
  "recipientName": "Nguyen Van A",
  "phone": "0901234567",
  "label": "Home",
  "provinceId": 202,
  "provinceName": "Ho Chi Minh",
  "districtId": 1442,
  "districtName": "Quan 1",
  "wardCode": "20101",
  "wardName": "Phuong Ben Nghe",
  "addressLine": "123 Le Loi",
  "isDefault": true,
  "createdAt": "2026-11-10T08:00:00Z",
  "updatedAt": "2026-11-11T09:30:00Z"
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Thiếu hoặc token không hợp lệ. |
| `404` | `ADDRESS_NOT_FOUND` | "Address not found." | Không tìm thấy địa chỉ hoặc địa chỉ không thuộc quyền sở hữu của người dùng. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IAddressService.cs` và `AddressService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `MeController.cs` (`PATCH /api/v1/me/addresses/{addressId}/default`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test User.sln -c Release` vượt qua 100%.
