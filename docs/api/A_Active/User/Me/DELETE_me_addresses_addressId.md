# [DELETE] /api/v1/me/addresses/{addressId}

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Address`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Xóa mềm một địa chỉ giao hàng thuộc sở hữu của người dùng hiện tại.
* **HTTP Method:** `DELETE`
* **Đường dẫn chuẩn:** `/api/v1/me/addresses/{addressId}`
* **Quyền hạn truy cập (RBAC):** `Authenticated` (`[RequireRole]`) + Resource Ownership (`UserId == currentUserId`).

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/MeController.cs` $\rightarrow$ `DeleteMyAddress()`
* **Application Interface:** `User.Application/Services/Address/IAddressService.cs` $\rightarrow$ `DeleteAddressAsync()`
* **Service Implementation:** `User.Application/Services/Address/AddressService.cs` $\rightarrow$ `DeleteAddressAsync()`
* **Thực thể & Bảng DB:** `User.Domain/Entities/UserAddress.cs` $\rightarrow$ Bảng `UserAddresses` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters
| Tên tham số | Kiểu dữ liệu | Bắt buộc | Ràng buộc & Mô tả |
|:---|:---|:---:|:---|
| `addressId` | `Guid` | Có | Định danh địa chỉ cần xóa (`UserAddress.Id`). |

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

* **Mã HTTP thành công:** `204 No Content` (Không trả về body).

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Thiếu hoặc token không hợp lệ. |
| `404` | `ADDRESS_NOT_FOUND` | "Address not found." | Không tìm thấy địa chỉ hoặc địa chỉ không thuộc quyền sở hữu của người dùng. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IAddressService.cs` và `AddressService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `MeController.cs` (`DELETE /api/v1/me/addresses/{addressId}`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test User.sln -c Release` vượt qua 100%.
