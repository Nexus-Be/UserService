# [POST] /api/v1/sellers/registration/documents

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Seller`  
> **Mã tham chiếu Excel:** `32`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `✅ IMPLEMENTED`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Upload tài liệu Seller(CCCD, ....)
* **HTTP Method:** `POST`
* **Đường dẫn chuẩn:** `/api/v1/sellers/registration/documents`
* **Đường dẫn gốc trong Excel:** `/api/v1/seller/registration/documents` (Chuẩn hóa từ '/api/v1/seller/registration/documents' sang chuẩn 100% số nhiều '/api/v1/sellers/registration/documents'.)
* **Quyền hạn truy cập (RBAC):** `Authenticated (Buyer / Seller)` (`[RequireRole]` cho phép Buyer đang đăng ký hoặc Seller cập nhật hồ sơ)
* **Yêu cầu Idempotency:** Tùy chọn (Khuyến nghị gửi header `Idempotency-Key: UUIDv4` để tránh gửi lặp)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

Mọi thay đổi trong hàm nghiệp vụ (thêm validation, ném exception, thêm enum) bắt buộc phải cập nhật đồng bộ vào tài liệu này:

* **Microservice thụ lý:** `src/Services/User/`
* **Cổng Gateway điều phối:** `src/Services/Gateway/Gateway/appsettings.json` (Route: `sellers-plural-route`)
* **Controller Action:** `User.Api/Controllers/SellerController.cs` $\rightarrow$ `UploadSellerDocuments()`
* **Application Interface:** `User.Application/Services/Seller/ISellerService.cs` $\rightarrow$ `UploadSellerDocumentsAsync()`
* **Service Implementation:** `User.Application/Services/Seller/SellerService.cs` $\rightarrow$ `UploadSellerDocumentsAsync()`
* **Thực thể & Bảng DB:** `User.Domain/Entities/SellerProfile.cs` $\rightarrow$ Bảng `SellerProfiles` trong PostgreSQL
* **Unit Tests:** `User.Tests/SellerDocumentsTests.cs`
* **Typed HTTP Client (nếu có):** N/A (Giao tiếp nội bộ trực tiếp qua DbContext)

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters (Tham số đường dẫn)
*Không có tham số đường dẫn.*

### 3.2. Query Parameters (Tham số truy vấn)
*Không có tham số truy vấn.*

### 3.3. Request Body (JSON Payload)
```json
Header:
{
  "Authorization": "Bearer <JWT>"
}

Body (multipart/form-data):
{
  "registrationId": "uuid (optional — mặc định lấy hồ sơ seller mới nhất của user hiện tại)",
  "frontCccd": "binary (image/jpeg, image/png, application/pdf, max 5MB)",
  "backCccd": "binary (image/jpeg, image/png, application/pdf, max 5MB)",
  "businessLicense": "binary (optional — image/jpeg, image/png, application/pdf, max 5MB)"
}
```


---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE ENVELOPE)

### 4.1. Phản hồi Thành công Đơn lẻ (`ApiResponse<T>`)
Áp dụng cho API trả về 1 đối tượng hoặc kết quả thực thi:
```json
200 OK:
{
  "success": true,
  "statusCode": 200,
  "message": "Identity documents uploaded successfully.",
  "data": {
    "registrationId": "uuid | null",
    "idCardFrontUrl": "string (Cloudinary Signed URL)",
    "idCardBackUrl": "string (Cloudinary Signed URL)",
    "businessLicenseUrl": "string? (Cloudinary Signed URL)",
    "documentUrls": "string[]"
  },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "timestamp": "2026-10-26T00:00:00Z"
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

Frontend bắt buộc dựa vào cột `errorCode` để map và hiển thị giao diện phù hợp. Khi backend function bổ sung lệnh ném lỗi, phải cập nhật ngay vào bảng này:

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `400` | `VAL_INVALID_INPUT` | "One or more validation errors occurred." | Dữ liệu đầu vào vi phạm FluentValidation hoặc model binding. |
| `401` | `UNAUTHORIZED` | "Authentication token is missing, expired, or invalid." | Người dùng chưa đăng nhập hoặc JWT không hợp lệ. |
| `404` | `REGISTRATION_NOT_FOUND` | "Registration request was not found." | Điều kiện nghiệp vụ đặc thù: REGISTRATION_NOT_FOUND. |
| `401` | `UNAUTHORIZED` | "Authentication required, user is not logged in." | Điều kiện nghiệp vụ đặc thù: UNAUTHORIZED. |
| `400` | `INVALID_FILE_TYPE` | "Invalid document format, only JPG, PNG, and PDF are accepted." | Điều kiện nghiệp vụ đặc thù: INVALID_FILE_TYPE. |
| `413` | `FILE_TOO_LARGE` | "File size exceeds maximum allowed limit of 5MB." | Dung lượng file vượt quá 5MB. |
| `403` | `FORBIDDEN` | "You do not have permission to perform this action." | Người dùng không đủ quyền RBAC theo quy định. |
| `404` | `SELLERPROFILE_NOT_FOUND` | "Sellerprofile not found with the specified ID." | Không tìm thấy bản ghi trong Database tương ứng. |
| `500` | `INTERNAL_SERVER_ERROR` | "An unexpected error occurred. Please try again later." | Lỗi hệ thống nội bộ chưa được xử lý. |

---

## 6. DANH MỤC ENUM LIÊN QUAN (DOMAIN ENUMS)

*Endpoint này không sử dụng enum trực tiếp trong tham số đầu vào (Request) hoặc dữ liệu trả về (Response).*

---

## 7. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `ISellerService.cs` và `SellerService.cs` (`UploadSellerDocumentsAsync`) đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `SellerController.cs` (`UploadSellerDocuments`) ghép cặp 1-to-1, summary XML ngắn gọn.
- [x] **3. Bắt lỗi & Validation:** Toàn bộ mã lỗi trong ma trận ở Mục 5 đã được kiểm soát trong code.
- [x] **4. Enum Mapping:** Toàn bộ enum ở Mục 6 được lưu integer trong DB và serialize PascalCase ở API (N/A cho endpoint này).
- [x] **5. Kiểm thử tự động:** `dotnet test` vượt qua 100% các bài test (`SellerDocumentsTests.cs`).
- [ ] **6. Định tuyến Gateway:** Route YARP `sellers-plural-route` đã được cấu hình trong `appsettings.json` và test thông suốt.
- [ ] **7. Xác nhận của Sếp (Human Approval):** Đã kiểm thử thực tế và cho phép nghiệm thu chính thức.

> **Trạng thái nghiệm thu:** `[ ✅ IMPLEMENTED (Lưu tại A_Active/User — Chờ nghiệm thu Gateway & Sếp) ]`
