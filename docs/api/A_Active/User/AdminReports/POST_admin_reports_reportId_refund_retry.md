# [POST] /api/v1/admin/reports/{reportId}/refund/retry

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Report`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Admin hoặc Support Staff thử kích hoạt lại giao dịch hoàn tiền (refund) cho một đơn báo cáo vi phạm đã xử lý (`Resolved`) nhưng trạng thái hoàn tiền đang ở `Pending` hoặc `Failed`.
* **HTTP Method:** `POST`
* **Đường dẫn chuẩn:** `/api/v1/admin/reports/{reportId}/refund/retry`
* **Quyền hạn truy cập (RBAC):** `Admin` hoặc `Staff` (`[RequireRole("admin", "staff")]`)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/AdminReportsController.cs` $\rightarrow$ `RetryRefund()`
* **Application Interface:** `User.Application/Services/Report/IReportService.cs` $\rightarrow$ `RetryReportRefundAsync()`
* **Service Implementation:** `User.Application/Services/Report/ReportService.cs` $\rightarrow$ `RetryReportRefundAsync()`
* **Response DTO:** `User.Application/Services/Report/ReportResponses.cs` $\rightarrow$ `ResolveReportResponse`
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
  "reportedUserId": "cccccccc-3333-3333-3333-333333333333",
  "status": "Resolved",
  "targetRole": "Seller",
  "penaltyApplied": "Counterfeit",
  "scoreDeducted": 10,
  "reportedUserNewScore": 40,
  "penaltyId": "60000000-aaaa-bbbb-cccc-000000000009",
  "resolvedBy": "bbbbbbbb-2222-2222-2222-222222222222",
  "resolvedAt": "2026-11-07T03:00:00Z",
  "resolutionNote": "Confirmed counterfeit shipment.",
  "refundStatus": "Refunded",
  "refundAmount": 500000,
  "refundMessage": "Refund completed."
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Thiếu hoặc token không hợp lệ. |
| `403` | `FORBIDDEN` | "You do not have permission to access this resource." | Người dùng không có vai trò `admin` hoặc `staff`. |
| `404` | `REPORT_NOT_FOUND` | "Report not found." | Không tìm thấy đơn báo cáo theo `reportId`. |
| `409` | `INVALID_REPORT_STATE` | "Report is not eligible for refund retry." | Đơn báo cáo chưa ở trạng thái `Resolved` hoặc không ở trạng thái `Pending`/`Failed` để retry. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IReportService.cs` và `ReportService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `AdminReportsController.cs` (`POST /api/v1/admin/reports/{reportId}/refund/retry`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test User.sln -c Release` vượt qua 100%.
