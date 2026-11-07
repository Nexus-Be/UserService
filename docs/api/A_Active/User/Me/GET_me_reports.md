# [GET] /api/v1/me/reports

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Report`  
> **Mã tham chiếu Excel:** `29`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `✅ VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Người dùng xem danh sách các đơn báo cáo tố cáo vi phạm do chính mình đã gửi.
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/me/reports`
* **Quyền hạn truy cập (RBAC):** `Authenticated` (`[RequireRole]`)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/MeController.cs` $\rightarrow$ `GetMyReports()`
* **Application Interface:** `User.Application/Services/Report/IReportService.cs` $\rightarrow$ `GetMyReportsAsync()`
* **Service Implementation:** `User.Application/Services/Report/ReportService.cs` $\rightarrow$ `GetMyReportsAsync()`
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
      "status": "Resolved",
      "resolvedBy": "bbbbbbbb-2222-2222-2222-222222222222",
      "resolvedAt": "2026-11-07T03:00:00Z",
      "resolutionNote": "Confirmed counterfeit shipment via unboxing video evidence.",
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
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Người dùng chưa đăng nhập. |

---

## 6. DANH MỤC ENUM LIÊN QUAN (DOMAIN ENUMS)

| Tên trường Payload (JSON Field) | Kiểu C# Enum | Giá trị DB (`integer`) | Giá trị API (`string PascalCase`) | Ý nghĩa nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `status` | `ReportStatus` | `0`..`3` | `Pending`, `UnderReview`, `Resolved`, `Rejected` | Trạng thái xử lý đơn tố cáo. |
| `violationType` | `ReportViolationType` | `0`..`4` | `Fraud`, `Harassment`, `Counterfeit`, `Spam`, `Other` | Phân loại hành vi vi phạm. |

---

## 7. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IReportService.cs` và `ReportService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `MeController.cs` (`GET /api/v1/me/reports`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test` vượt qua 100% (`ReportServiceTests.cs`).

