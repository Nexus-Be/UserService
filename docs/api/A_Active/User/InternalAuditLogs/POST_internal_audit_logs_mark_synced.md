# [POST] /api/v1/internal/audit-logs/mark-synced

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Audit`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Đánh dấu các bản ghi `AuditLogs` là đã đồng bộ (`IsSynced = true`) sau khi Central Log Service đã lưu trữ thành công.
* **HTTP Method:** `POST`
* **Đường dẫn chuẩn:** `/api/v1/internal/audit-logs/mark-synced`
* **Quyền hạn truy cập (RBAC):** Internal Service-to-Service (`X-Internal-Service-Key`).

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Internal/InternalAuditLogsController.cs` $\rightarrow$ `MarkSynced()`
* **Application Interface:** `User.Application/Services/Audit/AuditSyncSourceService.cs` $\rightarrow$ `IAuditSyncSourceService.MarkSyncedAsync()`
* **Request / Response DTO:** `MarkAuditSyncedRequest` / `MarkAuditSyncedResponse`
* **Thực thể & Bảng DB:** `User.Domain/Entities/AuditLog.cs` $\rightarrow$ Bảng `AuditLogs` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Request Body (`MarkAuditSyncedRequest`)
```json
{
  "ids": [
    "90000000-aaaa-bbbb-cccc-000000000001",
    "90000000-aaaa-bbbb-cccc-000000000002"
  ]
}
```
* Tự động khử trùng (`Distinct()`) và giới hạn tối đa `500` ID mỗi lần gọi.

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "updated": 2
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `403` | `FORBIDDEN` | "Access denied to internal service resource." | Sai hoặc thiếu header `X-Internal-Service-Key`. |
| `500` | `INTERNAL_CONFIGURATION_ERROR` | "Internal service authentication is not configured." | Chưa cấu hình `InternalServiceKey` trong server. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `AuditSyncSourceService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `InternalAuditLogsController.cs` (`POST /api/v1/internal/audit-logs/mark-synced`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test User.sln -c Release` vượt qua 100%.
