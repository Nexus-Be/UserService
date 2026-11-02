# [PUT] /api/v1/admin/reports/{reportId}/resolve

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `AdminUsers`  
> **Mã tham chiếu Excel:** `41`  
> **Trạng thái tài liệu:** `B_none (Chờ hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `⏳ IN_PROGRESS`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Staff xử lý chấp thuận tố cáo & áp dụng chế tài kỷ luật
* **HTTP Method:** `PUT`
* **Đường dẫn chuẩn:** `/api/v1/admin/reports/{reportId}/resolve`
* **Đường dẫn gốc trong Excel:** `/api/v1/admin/reports/{reportId}/resolve` (Đường dẫn đã tuân thủ chuẩn 100% số nhiều.)
* **Quyền hạn truy cập (RBAC):** `StaffOrAdmin`
* **Yêu cầu Idempotency:** Tùy chọn (Khuyến nghị gửi header `Idempotency-Key: UUIDv4` để tránh gửi lặp)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

Mọi thay đổi trong hàm nghiệp vụ (thêm validation, ném exception, thêm enum) bắt buộc phải cập nhật đồng bộ vào tài liệu này:

* **Microservice thụ lý:** `src/Services/User/`
* **Cổng Gateway điều phối:** `src/Services/Gateway/Gateway/appsettings.json` (Route: `admin-user-route`)
* **Controller Action:** `User.Api/Controllers/AdminUsersController.cs` $\rightarrow$ `ResolveReport()`
* **Application Interface:** `User.Application/Services/AdminUsers/IAdminUserService.cs` $\rightarrow$ `ResolveReportAsync()`
* **Service Implementation:** `User.Application/Services/AdminUsers/AdminUserService.cs` $\rightarrow$ `ResolveReportAsync()`
* **Thực thể & Bảng DB:** `User.Domain/Entities/UserReport.cs` $\rightarrow$ Bảng `UserReports` trong PostgreSQL
* **Typed HTTP Client (nếu có):** N/A (Giao tiếp nội bộ trực tiếp qua DbContext)

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters (Tham số đường dẫn)
| Tên tham số | Kiểu dữ liệu | Bắt buộc? | Ràng buộc & Định dạng |
|:---|:---|:---:|:---|
| `reportId` | `Guid (UUID)` | Có | Mã định danh duy nhất (UUIDv4) |

### 3.2. Query Parameters (Tham số truy vấn)
*Không có tham số truy vấn.*

### 3.3. Request Body (JSON Payload)
```json
Header:
{
  "Authorization": "Bearer <JWT>"
}

Path:
{ "reportId": "uuid" }

Body:
{
"action": "string (\"DEDUCT_REPUTATION\" | \"BAN_USER\")",
"pointsDeducted": "int (optional: 1..50)",
"resolutionNote": "string (>= 10 ký tự)"
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
  "message": "Report resolved successfully and penalty applied.",
  "data": {
    "reportId": "uuid",
    "status": "Resolved",
    "resolvedAt": "timestamp"
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
| `404` | `REPORT_NOT_FOUND` | "Report was not found." | Điều kiện nghiệp vụ đặc thù: REPORT_NOT_FOUND. |
| `403` | `PERMISSION_DENIED` | "You do not have permission to process reports." | Điều kiện nghiệp vụ đặc thù: PERMISSION_DENIED. |
| `401` | `UNAUTHORIZED` | "Authentication required, user is not logged in." | Điều kiện nghiệp vụ đặc thù: UNAUTHORIZED. |
| `400` | `ALREADY_RESOLVED` | "Report has already been resolved or processed." | Điều kiện nghiệp vụ đặc thù: ALREADY_RESOLVED. |
| `403` | `FORBIDDEN` | "You do not have permission to perform this action." | Người dùng không đủ quyền RBAC theo quy định. |
| `404` | `USERREPORT_NOT_FOUND` | "Userreport not found with the specified ID." | Không tìm thấy bản ghi trong Database tương ứng. |
| `500` | `INTERNAL_SERVER_ERROR` | "An unexpected error occurred. Please try again later." | Lỗi hệ thống nội bộ chưa được xử lý. |

---

## 6. DANH MỤC ENUM LIÊN QUAN (DOMAIN ENUMS)

Bảng đối chiếu toàn diện giữa trường dữ liệu trong Payload (JSON Field), định dạng lưu trữ Database và chuỗi trả về API để Frontend hiển thị và binding giao diện chính xác:

| Tên trường Payload (JSON Field) | Kiểu C# Enum | Giá trị DB (`integer`) | Giá trị API (`string PascalCase`) | Ý nghĩa nghiệp vụ & Hướng dẫn hiển thị Frontend |
|:---|:---|:---:|:---|:---|
| `status` | `ReportStatus` | `0` | `Pending` | **Chờ tiếp nhận**: Đơn tố cáo mới gửi, đang chờ Staff phân công thụ lý. Badge vàng (Warning). |
| `status` | `ReportStatus` | `1` | `UnderReview` | **Đang xác minh**: Staff đang kiểm tra bằng chứng và đối chất. Badge xanh dương (Info). |
| `status` | `ReportStatus` | `2` | `Resolved` | **Đã xử lý**: Đã có kết luận và áp dụng chế tài kỷ luật. Badge xanh lá (Success). |
| `status` | `ReportStatus` | `3` | `Rejected` | **Bác bỏ**: Đơn tố cáo không đủ chứng cứ hoặc sai sự thật. Badge xám (Muted). |
| `violationType` | `ReportViolationType` | `0` | `Fraud` | **Lừa đảo**: Gian lận tiền bạc, không giao hàng hoặc hàng giả. Badge đỏ (Danger). |
| `violationType` | `ReportViolationType` | `1` | `Harassment` | **Quấy rối / Xúc phạm**: Ngôn từ đe dọa, xúc phạm danh dự đối tác. Badge cam (Warning). |
| `violationType` | `ReportViolationType` | `2` | `Counterfeit` | **Hàng giả / Nhái**: Bán hàng vi phạm sở hữu trí tuệ. Badge đỏ (Danger). |
| `violationType` | `ReportViolationType` | `3` | `Spam` | **Spam / Rác**: Đăng bài rác, gửi tin nhắn quảng cáo phiền toái. Badge vàng (Warning). |
| `violationType` | `ReportViolationType` | `4` | `Other` | **Vi phạm khác**: Các lý do vi phạm khác quy chế sàn. Badge xám (Secondary). |

---

## 7. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [ ] **1. Tầng Application:** `IAdminUserService.cs` và `AdminUserService.cs` đã được triển khai hoàn chỉnh.
- [ ] **2. Tầng Api:** `AdminUsersController.cs` ghép cặp 1-to-1, route số nhiều, summary XML ngắn gọn.
- [ ] **3. Bắt lỗi & Validation:** Toàn bộ mã lỗi trong ma trận ở Mục 5 đã được kiểm soát trong code.
- [ ] **4. Enum Mapping:** Toàn bộ enum ở Mục 6 được lưu integer trong DB và serialize PascalCase ở API.
- [ ] **5. Kiểm thử tự động:** `dotnet test` vượt qua 100% các bài test (Kèm tên file test: `AdminUsersTests.cs`).
- [ ] **6. Định tuyến Gateway:** Route YARP `admin-user-route` đã được cấu hình trong `appsettings.json` và test thông suốt.
- [ ] **7. Xác nhận của Sếp (Human Approval):** Đã kiểm thử thực tế và cho phép chuyển sang `docs/api/A_Active/`.

> **Trạng thái nghiệm thu:** `[ ⏳ IN_PROGRESS (Lưu tại B_none) ]`
