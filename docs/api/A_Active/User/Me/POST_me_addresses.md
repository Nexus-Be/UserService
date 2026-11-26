# [POST] /api/v1/me/addresses

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Address`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Tạo mới một địa chỉ giao hàng cho người dùng hiện tại kèm mã địa giới hành chính GHN (`provinceId`, `districtId`, `wardCode`).
* **HTTP Method:** `POST`
* **Đường dẫn chuẩn:** `/api/v1/me/addresses`
* **Quyền hạn truy cập (RBAC):** `Authenticated` (`[RequireRole]`)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/MeController.cs` $\rightarrow$ `CreateMyAddress()`
* **Application Interface:** `User.Application/Services/Address/IAddressService.cs` $\rightarrow$ `CreateAddressAsync()`
* **Service Implementation:** `User.Application/Services/Address/AddressService.cs` $\rightarrow$ `CreateAddressAsync()`
* **Request DTO:** `User.Application/Services/Address/AddressRequests.cs` $\rightarrow$ `CreateAddressRequest`
* **Response DTO:** `User.Application/Services/Address/AddressResponses.cs` $\rightarrow$ `UserAddressDto`
* **Thực thể & Bảng DB:** `User.Domain/Entities/UserAddress.cs` $\rightarrow$ Bảng `UserAddresses` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Request Body (`CreateAddressRequest`)
```json
{
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
  "isDefault": true
}
```
* `recipientName`: Bắt buộc, tối đa 150 ký tự.
* `phone`: Bắt buộc, tối đa 30 ký tự.
* `label`: Không bắt buộc, tối đa 60 ký tự.
* `provinceId`, `districtId`: Số nguyên dương (`>= 1`).
* `provinceName`, `districtName`, `wardName`: Bắt buộc, tối đa 150 ký tự.
* `wardCode`: Bắt buộc, tối đa 50 ký tự.
* `addressLine`: Bắt buộc, tối đa 500 ký tự.
* `isDefault`: `bool`, mặc định `false`.

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
201 Created:
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
  "updatedAt": "2026-11-10T08:00:00Z"
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `400` | `VAL_INVALID_INPUT` | "One or more validation errors occurred." | Dữ liệu đầu vào vi phạm DataAnnotations. |
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Thiếu hoặc token không hợp lệ. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IAddressService.cs` và `AddressService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `MeController.cs` (`POST /api/v1/me/addresses`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test User.sln -c Release` vượt qua 100%.
