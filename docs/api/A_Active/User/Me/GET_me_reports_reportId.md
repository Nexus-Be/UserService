# [GET] /api/v1/me/reports/{reportId}

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Report`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Người dùng xem chi tiết một đơn báo cáo/tố cáo vi phạm do chính mình đã gửi.
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/me/reports/{reportId}`
* **Quyền hạn truy cập (RBAC):** `Authenticated` (`[RequireRole]`) + Resource Ownership (`ReporterId == currentUserId`).

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/MeController.cs` $\rightarrow$ `GetMyReportById()`
* **Application Interface:** `User.Application/Services/Report/IReportService.cs` $\rightarrow$ `GetMyReportByIdAsync()`
* **Service Implementation:** `User.Application/Services/Report/ReportService.cs` $\rightarrow$ `GetMyReportByIdAsync()`
* **Response DTO:** `User.Application/Services/Report/ReportDtos.cs` $\rightarrow$ `ReportListItemDto`
* **Thực thể & Bảng DB:** `User.Domain/Entities/UserReport.cs` $\rightarrow$ Bảng `UserReports` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters
| Tên tham số | Kiểu dữ liệu | Bắt buộc | Ràng buộc & Mô tả |
|:---|:---|:---:|:---|
| `reportId` | `Guid` | Có | Định danh đơn báo cáo vi phạm (`UserReport.Id`). |

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
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
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Thiếu hoặc token không hợp lệ. |
| `403` | `FORBIDDEN` | "You do not have permission to view this report." | Người dùng hiện tại không phải là người đã gửi đơn báo cáo này. |
| `404` | `REPORT_NOT_FOUND` | "Report not found." | Không tìm thấy đơn báo cáo theo `reportId`. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IReportService.cs` và `ReportService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `MeController.cs` (`GET /api/v1/me/reports/{reportId}`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test User.sln -c Release` vượt qua 100%.
