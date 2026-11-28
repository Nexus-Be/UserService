# [PUT] /api/v1/me/addresses/{addressId}

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Address`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Cập nhật thông tin một địa chỉ giao hàng thuộc sở hữu của người dùng hiện tại.
* **HTTP Method:** `PUT`
* **Đường dẫn chuẩn:** `/api/v1/me/addresses/{addressId}`
* **Quyền hạn truy cập (RBAC):** `Authenticated` (`[RequireRole]`) + Resource Ownership (`UserId == currentUserId`).

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/MeController.cs` $\rightarrow$ `UpdateMyAddress()`
* **Application Interface:** `User.Application/Services/Address/IAddressService.cs` $\rightarrow$ `UpdateAddressAsync()`
* **Service Implementation:** `User.Application/Services/Address/AddressService.cs` $\rightarrow$ `UpdateAddressAsync()`
* **Request DTO:** `User.Application/Services/Address/AddressRequests.cs` $\rightarrow$ `UpdateAddressRequest`
* **Response DTO:** `User.Application/Services/Address/AddressResponses.cs` $\rightarrow$ `UserAddressDto`
* **Thực thể & Bảng DB:** `User.Domain/Entities/UserAddress.cs` $\rightarrow$ Bảng `UserAddresses` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters
| Tên tham số | Kiểu dữ liệu | Bắt buộc | Ràng buộc & Mô tả |
|:---|:---|:---:|:---|
| `addressId` | `Guid` | Có | Định danh địa chỉ cần cập nhật (`UserAddress.Id`). |

### 3.2. Request Body (`UpdateAddressRequest`)
```json
{
  "recipientName": "Nguyen Van A",
  "phone": "0901234567",
  "label": "Office",
  "provinceId": 202,
  "provinceName": "Ho Chi Minh",
  "districtId": 1442,
  "districtName": "Quan 1",
  "wardCode": "20101",
  "wardName": "Phuong Ben Nghe",
  "addressLine": "456 Nguyen Hue",
  "isDefault": false
}
```

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "id": "11111111-aaaa-bbbb-cccc-000000000001",
  "userId": "dddddddd-4444-4444-4444-444444444444",
  "recipientName": "Nguyen Van A",
  "phone": "0901234567",
  "label": "Office",
  "provinceId": 202,
  "provinceName": "Ho Chi Minh",
  "districtId": 1442,
  "districtName": "Quan 1",
  "wardCode": "20101",
  "wardName": "Phuong Ben Nghe",
  "addressLine": "456 Nguyen Hue",
  "isDefault": false,
  "createdAt": "2026-11-10T08:00:00Z",
  "updatedAt": "2026-11-11T09:00:00Z"
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `400` | `VAL_INVALID_INPUT` | "One or more validation errors occurred." | Dữ liệu đầu vào vi phạm DataAnnotations. |
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Thiếu hoặc token không hợp lệ. |
| `404` | `ADDRESS_NOT_FOUND` | "Address not found." | Không tìm thấy địa chỉ hoặc địa chỉ không thuộc quyền sở hữu của người dùng. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IAddressService.cs` và `AddressService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `MeController.cs` (`PUT /api/v1/me/addresses/{addressId}`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test User.sln -c Release` vượt qua 100%.
