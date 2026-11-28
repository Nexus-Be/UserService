# [POST] /api/v1/users/reports

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Report`  
> **Mã tham chiếu Excel:** `28`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `✅ VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Người dùng gửi đơn tố cáo tài khoản đối tác vi phạm quy chế sàn (kèm chỉ định nhánh vi phạm `TargetRole`: `Buyer`, `Seller`, hoặc `Account` và mã giao dịch tham chiếu `ReferenceId`).
* **HTTP Method:** `POST`
* **Đường dẫn chuẩn:** `/api/v1/users/reports`
* **Quyền hạn truy cập (RBAC):** `Authenticated` (`[RequireRole]`)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/UsersController.cs` $\rightarrow$ `CreateUserReport()`
* **Application Interface:** `User.Application/Services/Report/IReportService.cs` $\rightarrow$ `CreateReportAsync()`
* **Service Implementation:** `User.Application/Services/Report/ReportService.cs` $\rightarrow$ `CreateReportAsync()`
* **Request / Response DTOs:** `User.Application/Services/Report/ReportRequests.cs` (`CreateReportRequest`), `ReportResponses.cs` (`ReportDetailResponse`)
* **Thực thể & Bảng DB:** `User.Domain/Entities/UserReport.cs` $\rightarrow$ Bảng `UserReports` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Request Body (JSON Payload)
```json
{
  "reportedUserId": "cccccccc-3333-3333-3333-333333333333",
  "targetRole": "Seller",
  "referenceType": "Order",
  "referenceId": "ORD-FRAUD-99",
  "violationType": "Counterfeit",
  "description": "Seller shipped counterfeit replica headphones instead of authentic product.",
  "evidenceUrls": [
    "https://images.nexus.local/reports/evidence1.png"
  ]
}
```

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
201 Created:
{
  "reportId": "50000000-aaaa-bbbb-cccc-000000000001",
  "reporterId": "dddddddd-4444-4444-4444-444444444444",
  "reportedUserId": "cccccccc-3333-3333-3333-333333333333",
  "targetRole": "Seller",
  "referenceType": "Order",
  "referenceId": "ORD-FRAUD-99",
  "violationType": "Counterfeit",
  "description": "Seller shipped counterfeit replica headphones instead of authentic product.",
  "evidenceUrls": [
    "https://images.nexus.local/reports/evidence1.png"
  ],
  "status": "Pending",
  "createdAt": "2026-11-07T03:00:00Z"
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `400` | `VAL_INVALID_INPUT` | "Report description must be at least 10 characters long." | Nội dung mô tả ngắn hơn 10 ký tự hoặc dữ liệu đầu vào không hợp lệ. |
| `400` | `SELF_REPORT_NOT_ALLOWED` | "You cannot submit a violation report against your own account." | `reporterId == reportedUserId`. |
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Người dùng chưa đăng nhập. |
| `404` | `USER_NOT_FOUND` | "Reported user account was not found." | Không tìm thấy tài khoản bị tố cáo. |
| `409` | `DUPLICATE_REPORT` | "An active report already exists for this user and transaction reference." | Đã tồn tại đơn tố cáo đang ở trạng thái `Pending` hoặc `UnderReview` cho cùng đối tượng và giao dịch. |

---

## 6. DANH MỤC ENUM LIÊN QUAN (DOMAIN ENUMS)

| Tên trường Payload (JSON Field) | Kiểu C# Enum | Giá trị DB (`integer`) | Giá trị API (`string PascalCase`) | Ý nghĩa nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `targetRole` | `TargetRole` | `0`, `1`, `2` | `Buyer`, `Seller`, `Account` | Nhánh quyền hạn của đối tượng bị tố cáo. |
| `status` | `ReportStatus` | `0`..`3` | `Pending`, `UnderReview`, `Resolved`, `Rejected` | Trạng thái xử lý đơn tố cáo. |
| `violationType` | `ReportViolationType` | `0`..`4` | `Fraud`, `Harassment`, `Counterfeit`, `Spam`, `Other` | Phân loại hành vi vi phạm. |

---

## 7. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IReportService.cs` và `ReportService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `UsersController.cs` (`POST /api/v1/users/reports`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test` vượt qua 100% (`ReportServiceTests.cs`).

