# [GET] /api/v1/admin/audit-logs

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `AdminUsers`  
> **Mã tham chiếu Excel:** `56`  
> **Trạng thái tài liệu:** `B_none (Chờ hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `⏳ IN_PROGRESS`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Tra cứu Nhật ký Kiểm toán hệ thống
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/admin/audit-logs`
* **Đường dẫn gốc trong Excel:** `/api/v1/admin/audit-logs` (Đường dẫn đã tuân thủ chuẩn 100% số nhiều.)
* **Quyền hạn truy cập (RBAC):** `Admin`
* **Yêu cầu Idempotency:** Không áp dụng (Idempotent theo HTTP spec)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

Mọi thay đổi trong hàm nghiệp vụ (thêm validation, ném exception, thêm enum) bắt buộc phải cập nhật đồng bộ vào tài liệu này:

* **Microservice thụ lý:** `src/Services/User/`
* **Cổng Gateway điều phối:** `src/Services/Gateway/Gateway/appsettings.json` (Route: `admin-user-route`)
* **Controller Action:** `User.Api/Controllers/AdminUsersController.cs` $\rightarrow$ `GetAuditLogs()`
* **Application Interface:** `User.Application/Services/AdminUsers/IAdminUserService.cs` $\rightarrow$ `GetAuditLogsAsync()`
* **Service Implementation:** `User.Application/Services/AdminUsers/AdminUserService.cs` $\rightarrow$ `GetAuditLogsAsync()`
* **Thực thể & Bảng DB:** `User.Domain/Entities/AuditLog.cs` $\rightarrow$ Bảng `AuditLogs` trong PostgreSQL
* **Typed HTTP Client (nếu có):** N/A (Giao tiếp nội bộ trực tiếp qua DbContext)

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters (Tham số đường dẫn)
*Không có tham số đường dẫn.*

### 3.2. Query Parameters (Tham số truy vấn)
| Tên tham số | Kiểu dữ liệu | Mặc định | Ràng buộc & Khoảng giá trị |
|:---|:---|:---:|:---|
| `page` | `integer` | `1` | Số trang (tối thiểu 1) |
| `pageSize` | `integer` | `20` | Kích thước trang ($1 \le pageSize \le 100$) |

### 3.3. Request Body (JSON Payload)
```json
Header:
{
  "Authorization": "Bearer <JWT>"
}

Query:
{
  "page": 1,
  "limit": 20,
  "actorId": "uuid",
  "action": "string",
  "fromDate": "2026-10-01T00:00:00Z",
  "toDate": "2026-10-26T00:00:00Z"
}
```


---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE ENVELOPE)

### 4.1. Phản hồi Phân trang (`ApiResponse<PagedData<T>>`)
Áp dụng cho API tra cứu danh sách có phân trang:
```json
200 OK:
{
  "success": true,
  "statusCode": 200,
  "data": {
    "items": [
      {
        "id": "uuid",
        "actorId": "uuid",
        "action": "USER_BANNED",
        "targetEntity": "users",
        "oldValues": {},
        "newValues": {},
        "reason": "Policy violation.",
        "ipAddress": "127.0.0.1",
        "createdAt": "2026-10-26T00:00:00Z"
      }
    ],
    "pagination": {
      "pageIndex": 1,
      "pageSize": 20,
      "totalCount": 1,
      "totalPages": 1,
      "hasPreviousPage": false,
      "hasNextPage": false
    }
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
| `403` | `FORBIDDEN` | "User does not have permission to view audit logs." | Điều kiện nghiệp vụ đặc thù: FORBIDDEN. |
| `401` | `UNAUTHORIZED` | "Token is missing, expired, or invalid." | Điều kiện nghiệp vụ đặc thù: UNAUTHORIZED. |
| `403` | `FORBIDDEN` | "You do not have permission to perform this action." | Người dùng không đủ quyền RBAC theo quy định. |
| `404` | `AUDITLOG_NOT_FOUND` | "Auditlog not found with the specified ID." | Không tìm thấy bản ghi trong Database tương ứng. |
| `500` | `INTERNAL_SERVER_ERROR` | "An unexpected error occurred. Please try again later." | Lỗi hệ thống nội bộ chưa được xử lý. |

---

## 6. DANH MỤC ENUM LIÊN QUAN (DOMAIN ENUMS)

*Endpoint này không sử dụng enum trực tiếp trong tham số đầu vào (Request) hoặc dữ liệu trả về (Response).*

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
