# [GET] /api/v1/admin/sellers/{sellerId}

> **Tác giả:** Hiển Vinh & AI Agent  
> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `AdminSellers`  
> **Mã tham chiếu Excel:** `50` (`SELLER-09`)  
> **Trạng thái tài liệu:** `A_Active (Đã nghiệm thu)`  
> **Trạng thái triển khai:** `✅ COMPLETED`  
> **Cập nhật lần cuối:** 2026-11-02  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Quản trị viên (Admin) hoặc Nhân viên hỗ trợ (Staff) tra cứu thông tin hồ sơ chi tiết và hồ sơ KYC của người bán (Seller) theo Seller ID hoặc User ID.
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/admin/sellers/{sellerId}`
* **Quyền hạn truy cập (RBAC):** `RequireRole("admin", "staff")`
* **Yêu cầu Idempotency:** Không áp dụng (Idempotent theo HTTP spec)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `src/Services/User/`
* **Cổng Gateway điều phối:** YARP Route `admin-sellers-route` $\rightarrow$ `/api/v1/admin/sellers/{sellerId}`
* **Controller Action:** `User.Api/Controllers/Public/AdminSellersController.cs` $\rightarrow$ `GetSellerProfile(Guid sellerId)`
* **Application Interface:** `User.Application/Services/Seller/ISellerService.cs` $\rightarrow$ `GetAdminSellerProfileAsync(Guid sellerId, CancellationToken cancellationToken)`
* **Service Implementation:** `User.Application/Services/Seller/SellerService.cs` $\rightarrow$ `GetAdminSellerProfileAsync(...)`
* **Response DTO:** `User.Application/Services/Seller/SellerResponses.cs` $\rightarrow$ `AdminSellerProfileResponseDto`
* **Thực thể & Bảng DB:**
  - `User.Domain/Entities/SellerProfile.cs` $\rightarrow$ `SellerProfiles`
  - `User.Domain/Entities/User.cs` $\rightarrow$ `Users`
* **Unit Tests:** `User.Tests/AdminSellersControllerRestTests.cs`

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters (Tham số đường dẫn)
| Tên tham số | Kiểu dữ liệu | Bắt buộc? | Ràng buộc & Định dạng |
|:---|:---|:---:|:---|
| `sellerId` | `Guid (UUID)` | Có | Mã định danh người bán (`SellerProfiles.Id`) hoặc mã tài khoản người dùng (`Users.Id`). Không được để trống `Guid.Empty`. |

### 3.2. Query Parameters (Tham số truy vấn)
*Không có tham số truy vấn.*

### 3.3. Request Headers
```http
GET /api/v1/admin/sellers/11111111-1111-1111-1111-111111111111 HTTP/1.1
Host: localhost:8001
Authorization: Bearer <JWT_ACCESS_TOKEN>
```

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE ENVELOPE)

### 4.1. Phản hồi Thành công (`200 OK`)
Theo quy chuẩn Pure DTO tại microservice (Gateway tự bọc `ApiResponse<T>`):
```json
{
  "sellerId": "11111111-1111-1111-1111-111111111111",
  "userId": "22222222-2222-2222-2222-222222222222",
  "shopName": "Green Garden Store",
  "avatarUrl": "https://cdn.example.com/avatars/shop1.png",
  "businessEmail": "seller@greengarden.com",
  "hotline": "0901234567",
  "status": "Approved",
  "rejectionReason": null,
  "pickupAddress": "123 Garden Street, Ward 5",
  "provinceId": 201,
  "districtId": 1442,
  "wardCode": "20101",
  "idCardFrontUrl": "https://cdn.example.com/kyc/front.jpg",
  "idCardBackUrl": "https://cdn.example.com/kyc/back.jpg",
  "businessLicenseUrl": "https://cdn.example.com/kyc/license.pdf",
  "ownerFullName": "Nguyen Van A",
  "ownerIdentityNumber": "079090123456",
  "ownerPhoneNumber": "0901234567",
  "ownerReputationScore": 100,
  "ownerTrustLevel": "Good",
  "rating": 5.0,
  "totalReviews": 0,
  "bankInfo": {
    "bankName": "Unspecified",
    "accountNumber": "Unspecified",
    "accountHolderName": "Nguyen Van A"
  },
  "settlementInfo": {
    "taxCode": "Unspecified",
    "invoiceEmail": "seller@greengarden.com",
    "settlementCycle": "Weekly"
  },
  "reviewedBy": null,
  "reviewedAt": null,
  "createdAt": "2026-11-01T10:00:00Z",
  "updatedAt": "2026-11-01T10:00:00Z"
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code |
|:---:|:---|:---|:---|
| `400` | `VAL_INVALID_INPUT` | "Seller ID cannot be empty." | `sellerId == Guid.Empty` |
| `401` | `UNAUTHORIZED` | "Authentication token is missing, expired, or invalid." | Người dùng chưa đăng nhập hoặc token không hợp lệ |
| `403` | `FORBIDDEN` | "You do not have permission to perform this action." | Người dùng thiếu vai trò `admin` hoặc `staff` |
| `404` | `SELLER_NOT_FOUND` | "Seller profile was not found." | Không tìm thấy hồ sơ người bán với ID đã chỉ định |
| `500` | `INTERNAL_SERVER_ERROR` | "An unexpected error occurred. Please try again later." | Lỗi máy chủ không mong muốn |

---

## 6. DANH MỤC ENUM LIÊN QUAN (DOMAIN ENUMS)

| Tên trường Payload | Kiểu C# Enum | Giá trị DB (`integer`) | Giá trị API (`string PascalCase`) | Ý nghĩa nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `status` | `SellerProfileStatus` | `0` | `Pending` | Chờ duyệt hồ sơ mở Shop |
| `status` | `SellerProfileStatus` | `1` | `Approved` | Đã duyệt hoạt động |
| `status` | `SellerProfileStatus` | `2` | `Rejected` | Bị từ chối mở Shop |
| `ownerTrustLevel` | `TrustLevel` | `0` | `Untrusted` | Chưa được đánh giá |
| `ownerTrustLevel` | `TrustLevel` | `1` | `Probation` | Thử việc / theo dõi |
| `ownerTrustLevel` | `TrustLevel` | `2` | `Standard` | Tiêu chuẩn |
| `ownerTrustLevel` | `TrustLevel` | `3` | `Good` | Tin cậy cao |
| `ownerTrustLevel` | `TrustLevel` | `4` | `Excellent` | Xuất sắc |

---

## 7. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `ISellerService.cs` và `SellerService.cs` đã triển khai `GetAdminSellerProfileAsync`.
- [x] **2. Tầng Api:** `AdminSellersController.cs` ghép cặp 1-to-1, route `api/v1/admin/sellers/{sellerId}`, role `[RequireRole("admin", "staff")]`.
- [x] **3. Bắt lỗi & Validation:** Mã lỗi `VAL_INVALID_INPUT`, `SELLER_NOT_FOUND`, `FORBIDDEN`, `UNAUTHORIZED` đúng chuẩn.
- [x] **4. Enum Mapping:** Toàn bộ enum được lưu integer trong DB và serialize PascalCase ở API.
- [x] **5. Kiểm thử tự động:** `AdminSellersControllerRestTests.cs` đạt 100% (301/301 tests passed).
- [x] **6. Tuân thủ Quy chuẩn:** 100% Inward-Only, không tham chiếu ra ngoài repo.

> **Trạng thái nghiệm thu:** `[ ✅ COMPLETED (Lưu tại A_Active) ]`
