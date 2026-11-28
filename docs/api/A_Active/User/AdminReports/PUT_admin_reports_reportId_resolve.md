# [PUT] /api/v1/admin/reports/{reportId}/resolve

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Report`  
> **Mã tham chiếu Excel:** `41`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `✅ VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Admin hoặc Support Staff giải quyết chấp thuận đơn tố cáo vi phạm và áp dụng chế tài kỷ luật đích danh theo nhánh vai trò (`TargetRole.Buyer` trừ vào `CurrentScore`, `TargetRole.Seller` trừ vào `SellerScore`, đình chỉ shop hoặc khóa tài khoản, và chỉ sinh bản ghi `UserPenalty` khi có áp dụng hình phạt/trừ điểm).
* **HTTP Method:** `PUT`
* **Đường dẫn chuẩn:** `/api/v1/admin/reports/{reportId}/resolve`
* **Quyền hạn truy cập (RBAC):** `Admin`, `Staff` (`[RequireRole("admin", "staff")]`)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/AdminReportsController.cs` $\rightarrow$ `ResolveReport()`
* **Application Interface:** `User.Application/Services/Report/IReportService.cs` $\rightarrow$ `ResolveReportAsync()`
* **Service Implementation:** `User.Application/Services/Report/ReportService.cs` $\rightarrow$ `ResolveReportAsync()`
* **Request / Response DTOs:** `User.Application/Services/Report/ReportRequests.cs` (`ResolveReportRequest`), `ReportResponses.cs` (`ResolveReportResponse`)
* **Thực thể & Bảng DB:** `UserReport`, `ReputationScore`, `ReputationLog`, `UserPenalty`, `AuditLog` (`AuditAction.ReportResolve`)

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters
| Tên tham số | Kiểu dữ liệu | Bắt buộc? | Ràng buộc & Định dạng |
|:---|:---|:---:|:---|
| `reportId` | `Guid (UUID)` | Có | Mã định danh đơn tố cáo |

### 3.2. Request Body (JSON Payload)
```json
{
  "targetRole": "Seller",
  "penaltyApplied": "Restriction",
  "scoreDeduction": 10,
  "banAccount": false,
  "suspendSellerShop": true,
  "durationDays": 14,
  "policyClauseCode": "TOS_COUNTERFEIT_3_2",
  "resolutionNote": "Confirmed counterfeit shipment via unboxing video evidence."
}
```

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "reportId": "50000000-aaaa-bbbb-cccc-000000000001",
  "reportedUserId": "cccccccc-3333-3333-3333-333333333333",
  "status": "Resolved",
  "targetRole": "Seller",
  "penaltyApplied": "Restriction",
  "scoreDeducted": 10,
  "reportedUserNewScore": 42,
  "penaltyId": "60000000-aaaa-bbbb-cccc-000000000002",
  "resolvedBy": "bbbbbbbb-2222-2222-2222-222222222222",
  "resolvedAt": "2026-11-07T03:00:00Z",
  "resolutionNote": "Confirmed counterfeit shipment via unboxing video evidence."
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `400` | `VAL_INVALID_INPUT` | "Resolution note must be at least 10 characters long." | Ghi chú kết luận xử lý ngắn hơn 10 ký tự. |
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Chưa đăng nhập. |
| `403` | `FORBIDDEN` | "You do not have permission to perform this action." | Không có quyền `admin` hoặc `staff`. |
| `404` | `REPORT_NOT_FOUND` | "User report was not found for the specified identifier." | Không tìm thấy đơn tố cáo theo `reportId`. |
| `404` | `USER_NOT_FOUND` | "Reported user account was not found." | Không tìm thấy người dùng bị tố cáo. |
| `409` | `REPORT_ALREADY_PROCESSED` | "This report has already been resolved or dismissed." | Đơn tố cáo đã ở trạng thái `Resolved` hoặc `Rejected`. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IReportService.cs` và `ReportService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `AdminReportsController.cs` (`PUT /api/v1/admin/reports/{reportId}/resolve`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test` vượt qua 100% (`ReportServiceTests.cs`).

