# [GET] /api/v1/me/penalties

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Users`  
> **Mã tham chiếu Excel:** `18`  
> **Trạng thái tài liệu:** `B_none (Chờ hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `⏳ IN_PROGRESS`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** xem thông tin phạt của bản thân
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/me/penalties`
* **Đường dẫn gốc trong Excel:** `/api/v1/me/penalties` (Contextual Singleton giữ nguyên theo chuẩn RFC 9110.)
* **Quyền hạn truy cập (RBAC):** `Buyer`
* **Yêu cầu Idempotency:** Không áp dụng (Idempotent theo HTTP spec)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

Mọi thay đổi trong hàm nghiệp vụ (thêm validation, ném exception, thêm enum) bắt buộc phải cập nhật đồng bộ vào tài liệu này:

* **Microservice thụ lý:** `src/Services/User/`
* **Cổng Gateway điều phối:** `src/Services/Gateway/Gateway/appsettings.json` (Route: `user-me-route`)
* **Controller Action:** `User.Api/Controllers/UsersController.cs` $\rightarrow$ `GetMePenalties()`
* **Application Interface:** `User.Application/Services/Users/IUserService.cs` $\rightarrow$ `GetMePenaltiesAsync()`
* **Service Implementation:** `User.Application/Services/Users/UserService.cs` $\rightarrow$ `GetMePenaltiesAsync()`
* **Thực thể & Bảng DB:** `User.Domain/Entities/UserPenalty.cs` $\rightarrow$ Bảng `UserPenalties` trong PostgreSQL
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
"page": "int (default: 1)",
"limit": "int (default: 20)"
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
  "message": "Penalty history retrieved successfully.",
  "data": {
    "items": [
      {
        "id": "uuid",
        "violationType": "string",
        "reason": "string",
        "penaltyApplied": "string",
        "pointsDeducted": "int",
        "status": "string",
        "issuedAt": "timestamp",
        "expiresAt": "timestamp"
      }
    ],
    "pagination": {
      "pageIndex": 1,
      "pageSize": 20,
      "totalCount": 100,
      "totalPages": 5,
      "hasPreviousPage": false,
      "hasNextPage": true
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
| `401` | `UNAUTHORIZED` | "Authentication required, session is missing or expired." | Điều kiện nghiệp vụ đặc thù: UNAUTHORIZED. |
| `403` | `FORBIDDEN` | "You do not have permission to perform this action." | Người dùng không đủ quyền RBAC theo quy định. |
| `404` | `USERPENALTY_NOT_FOUND` | "Userpenalty not found with the specified ID." | Không tìm thấy bản ghi trong Database tương ứng. |
| `500` | `INTERNAL_SERVER_ERROR` | "An unexpected error occurred. Please try again later." | Lỗi hệ thống nội bộ chưa được xử lý. |

---

## 6. DANH MỤC ENUM LIÊN QUAN (DOMAIN ENUMS)

Bảng đối chiếu toàn diện giữa trường dữ liệu trong Payload (JSON Field), định dạng lưu trữ Database và chuỗi trả về API để Frontend hiển thị và binding giao diện chính xác:

| Tên trường Payload (JSON Field) | Kiểu C# Enum | Giá trị DB (`integer`) | Giá trị API (`string PascalCase`) | Ý nghĩa nghiệp vụ & Hướng dẫn hiển thị Frontend |
|:---|:---|:---:|:---|:---|
| `status` | `PenaltyStatus` | `0` | `Active` | **Đang hiệu lực**: Chế tài đang được thi hành trên tài khoản. Badge đỏ (Danger). |
| `status` | `PenaltyStatus` | `1` | `Expired` | **Đã hết hạn**: Thời gian phạt đã kết thúc, quyền lợi tự động phục hồi. Badge xám (Muted). |
| `status` | `PenaltyStatus` | `2` | `Revoked` | **Đã hủy bỏ / Ân xá**: Ban quản trị xem xét khiếu nại và gỡ bỏ án phạt. Badge xanh lá (Success). |
| `penaltyType` | `PenaltyType` | `0` | `Warning` | **Cảnh cáo**: Nhắc nhở người dùng bằng thông báo hệ thống. Badge vàng (Warning). |
| `penaltyType` | `PenaltyType` | `1` | `DeductPoints` | **Trừ điểm uy tín**: Khấu trừ điểm uy tín theo mức độ vi phạm. Badge cam (Warning). |
| `penaltyType` | `PenaltyType` | `2` | `SuspendBidding3Days` | **Tạm đình chỉ đấu giá 3 ngày**: Khóa quyền đặt giá và tạo phiên trong 72 giờ. Badge đỏ (Danger). |
| `penaltyType` | `PenaltyType` | `3` | `BanAccount` | **Khóa tài khoản vĩnh viễn**: Cấm đăng nhập và truy cập toàn bộ dịch vụ. Badge đỏ đậm (Dark Danger). |
| `penaltyType` | `PenaltyType` | `4` | `Other` | **Chế tài khác**: Hình thức phạt tùy chỉnh của ban quản trị. Badge xám (Secondary). |

---

## 7. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [ ] **1. Tầng Application:** `IUserService.cs` và `UserService.cs` đã được triển khai hoàn chỉnh.
- [ ] **2. Tầng Api:** `UsersController.cs` ghép cặp 1-to-1, route số nhiều, summary XML ngắn gọn.
- [ ] **3. Bắt lỗi & Validation:** Toàn bộ mã lỗi trong ma trận ở Mục 5 đã được kiểm soát trong code.
- [ ] **4. Enum Mapping:** Toàn bộ enum ở Mục 6 được lưu integer trong DB và serialize PascalCase ở API.
- [ ] **5. Kiểm thử tự động:** `dotnet test` vượt qua 100% các bài test (Kèm tên file test: `UsersTests.cs`).
- [ ] **6. Định tuyến Gateway:** Route YARP `user-me-route` đã được cấu hình trong `appsettings.json` và test thông suốt.
- [ ] **7. Xác nhận của Sếp (Human Approval):** Đã kiểm thử thực tế và cho phép chuyển sang `docs/api/A_Active/`.

> **Trạng thái nghiệm thu:** `[ ⏳ IN_PROGRESS (Lưu tại B_none) ]`
