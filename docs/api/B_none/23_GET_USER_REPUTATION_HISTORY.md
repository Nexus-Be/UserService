# [GET] /api/v1/users/{userId}/reputation/history

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Users`  
> **Mã tham chiếu Excel:** `23`  
> **Trạng thái tài liệu:** `B_none (Chờ hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `⏳ IN_PROGRESS`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Xem lịch sử uy tín (tăng or giảm)
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/users/{userId}/reputation/history`
* **Đường dẫn gốc trong Excel:** `/api/v1/user/{userId}/reputation/history` (Chuẩn hóa từ '/api/v1/user/{userId}/reputation/history' sang chuẩn 100% số nhiều '/api/v1/users/{userId}/reputation/history'.)
* **Quyền hạn truy cập (RBAC):** `Buyer`
* **Yêu cầu Idempotency:** Không áp dụng (Idempotent theo HTTP spec)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

Mọi thay đổi trong hàm nghiệp vụ (thêm validation, ném exception, thêm enum) bắt buộc phải cập nhật đồng bộ vào tài liệu này:

* **Microservice thụ lý:** `src/Services/User/`
* **Cổng Gateway điều phối:** `src/Services/Gateway/Gateway/appsettings.json` (Route: `users-plural-route`)
* **Controller Action:** `User.Api/Controllers/UsersController.cs` $\rightarrow$ `GetUserReputationHistory()`
* **Application Interface:** `User.Application/Services/Users/IUserService.cs` $\rightarrow$ `GetUserReputationHistoryAsync()`
* **Service Implementation:** `User.Application/Services/Users/UserService.cs` $\rightarrow$ `GetUserReputationHistoryAsync()`
* **Thực thể & Bảng DB:** `User.Domain/Entities/ReputationScore.cs` $\rightarrow$ Bảng `ReputationScores` trong PostgreSQL
* **Typed HTTP Client (nếu có):** N/A (Giao tiếp nội bộ trực tiếp qua DbContext)

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters (Tham số đường dẫn)
| Tên tham số | Kiểu dữ liệu | Bắt buộc? | Ràng buộc & Định dạng |
|:---|:---|:---:|:---|
| `userId` | `Guid (UUID)` | Có | Mã định danh duy nhất (UUIDv4) |

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

Path:
{ "userId": "uuid" }

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
  "message": "Reputation history retrieved successfully.",
  "data": {
    "items": [
      {
        "id": "int",
        "changeAmount": "int",
        "scoreAfter": "int",
        "reason": "string",
        "referenceType": "string",
        "createdAt": "timestamp"
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
| `403` | `FORBIDDEN` | "You do not have permission to view other users reputation history." | Điều kiện nghiệp vụ đặc thù: FORBIDDEN. |
| `401` | `UNAUTHORIZED` | "Authentication required, user is not logged in." | Điều kiện nghiệp vụ đặc thù: UNAUTHORIZED. |
| `403` | `FORBIDDEN` | "You do not have permission to perform this action." | Người dùng không đủ quyền RBAC theo quy định. |
| `404` | `REPUTATIONSCORE_NOT_FOUND` | "Reputationscore not found with the specified ID." | Không tìm thấy bản ghi trong Database tương ứng. |
| `500` | `INTERNAL_SERVER_ERROR` | "An unexpected error occurred. Please try again later." | Lỗi hệ thống nội bộ chưa được xử lý. |

---

## 6. DANH MỤC ENUM LIÊN QUAN (DOMAIN ENUMS)

Bảng đối chiếu toàn diện giữa trường dữ liệu trong Payload (JSON Field), định dạng lưu trữ Database và chuỗi trả về API để Frontend hiển thị và binding giao diện chính xác:

| Tên trường Payload (JSON Field) | Kiểu C# Enum | Giá trị DB (`integer`) | Giá trị API (`string PascalCase`) | Ý nghĩa nghiệp vụ & Hướng dẫn hiển thị Frontend |
|:---|:---|:---:|:---|:---|
| `reason` | `ReputationChangeReason` | `0` | `PositiveReview` | **Đánh giá tích cực (+ điểm)**: Nhận đánh giá 4-5 sao từ đối tác. Badge xanh lá (+). |
| `reason` | `ReputationChangeReason` | `1` | `NegativeReview` | **Đánh giá tiêu cực (- điểm)**: Nhận đánh giá 1-2 sao từ đối tác. Badge đỏ (-). |
| `reason` | `ReputationChangeReason` | `2` | `OrderCompleted` | **Giao dịch hoàn tất thành công (+ điểm)**: Đơn hàng giao nhận trọn vẹn. Badge xanh lá (+). |
| `reason` | `ReputationChangeReason` | `3` | `AuctionPaymentTimeout` | **Quá hạn thanh toán đấu giá (- điểm)**: Không thanh toán đơn đấu giá đúng hạn. Badge đỏ (-). |
| `reason` | `ReputationChangeReason` | `4` | `DisputeCompensation` | **Bồi hoàn sau khiếu nại (+/- điểm)**: Điều chỉnh điểm sau giải quyết tranh chấp. Badge cam. |
| `reason` | `ReputationChangeReason` | `5` | `ManualAdjustment` | **Điều chỉnh thủ công từ Admin**: Quản trị viên can thiệp trực tiếp điểm uy tín. Badge xanh dương (Info). |
| `reason` | `ReputationChangeReason` | `6` | `ViolationPenalty` | **Phạt vi phạm quy chế sàn (- điểm)**: Bị xử phạt từ đơn tố cáo đã duyệt. Badge đỏ (-). |

---

## 7. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [ ] **1. Tầng Application:** `IUserService.cs` và `UserService.cs` đã được triển khai hoàn chỉnh.
- [ ] **2. Tầng Api:** `UsersController.cs` ghép cặp 1-to-1, route số nhiều, summary XML ngắn gọn.
- [ ] **3. Bắt lỗi & Validation:** Toàn bộ mã lỗi trong ma trận ở Mục 5 đã được kiểm soát trong code.
- [ ] **4. Enum Mapping:** Toàn bộ enum ở Mục 6 được lưu integer trong DB và serialize PascalCase ở API.
- [ ] **5. Kiểm thử tự động:** `dotnet test` vượt qua 100% các bài test (Kèm tên file test: `UsersTests.cs`).
- [ ] **6. Định tuyến Gateway:** Route YARP `users-plural-route` đã được cấu hình trong `appsettings.json` và test thông suốt.
- [ ] **7. Xác nhận của Sếp (Human Approval):** Đã kiểm thử thực tế và cho phép chuyển sang `docs/api/A_Active/`.

> **Trạng thái nghiệm thu:** `[ ⏳ IN_PROGRESS (Lưu tại B_none) ]`
