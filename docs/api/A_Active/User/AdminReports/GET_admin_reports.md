# [GET] /api/v1/admin/reports

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Report`  
> **Mã tham chiếu Excel:** `40`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `✅ VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Admin hoặc Support Staff xem danh sách các đơn tố cáo vi phạm tài khoản toàn hệ thống có phân trang và bộ lọc.
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/admin/reports`
* **Quyền hạn truy cập (RBAC):** `Admin`, `Staff` (`[RequireRole("admin", "staff")]`)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/AdminReportsController.cs` $\rightarrow$ `GetAdminReports()`
* **Application Interface:** `User.Application/Services/Report/IReportService.cs` $\rightarrow$ `GetAdminReportsAsync()`
* **Service Implementation:** `User.Application/Services/Report/ReportService.cs` $\rightarrow$ `GetAdminReportsAsync()`
* **Item DTO:** `User.Application/Services/Report/ReportDtos.cs` $\rightarrow$ `ReportListItemDto`
* **Thực thể & Bảng DB:** `User.Domain/Entities/UserReport.cs` $\rightarrow$ Bảng `UserReports` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Query Parameters
| Tên tham số | Kiểu dữ liệu | Mặc định | Ràng buộc & Khoảng giá trị |
|:---|:---|:---:|:---|
| `page` | `integer` | `1` | Số trang (tối thiểu 1) |
| `pageSize` | `integer` | `20` | Kích thước trang ($1 \le pageSize \le 100$) |
| `status` | `ReportStatus?` | `null` | Lọc theo trạng thái (`Pending`, `UnderReview`, `Resolved`, `Rejected`) |
| `violationType` | `ReportViolationType?` | `null` | Lọc theo loại vi phạm (`Fraud`, `Harassment`, `Counterfeit`, `Spam`, `Other`) |

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "items": [
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
      "resolvedBy": null,
      "resolvedAt": null,
      "resolutionNote": null,
      "createdAt": "2026-11-07T02:00:00Z"
    }
  ],
  "pagination": {
    "currentPage": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1,
    "hasPrevious": false,
    "hasNext": false
  }
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Chưa đăng nhập. |
| `403` | `FORBIDDEN` | "You do not have permission to perform this action." | Không có quyền `admin` hoặc `staff`. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IReportService.cs` và `ReportService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `AdminReportsController.cs` (`GET /api/v1/admin/reports`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test` vượt qua 100% (`ReportServiceTests.cs`).

