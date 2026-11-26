# [GET] /api/v1/internal/audit-logs/unsynced

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Audit`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Cho phép Central Log Service kéo (pull) danh sách các bản ghi `AuditLogs` chưa được đồng bộ (`IsSynced == false`), sắp xếp từ cũ nhất đến mới nhất.
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/internal/audit-logs/unsynced`
* **Quyền hạn truy cập (RBAC):** Internal Service-to-Service (`X-Internal-Service-Key`).

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Internal/InternalAuditLogsController.cs` $\rightarrow$ `GetUnsynced()`
* **Application Interface:** `User.Application/Services/Audit/AuditSyncSourceService.cs` $\rightarrow$ `IAuditSyncSourceService.GetUnsyncedAsync()`
* **Response DTO:** `AuditSyncPage`
* **Thực thể & Bảng DB:** `User.Domain/Entities/AuditLog.cs` $\rightarrow$ Bảng `AuditLogs` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Query Parameters
| Tên tham số | Kiểu dữ liệu | Mặc định | Ràng buộc & Mô tả |
|:---|:---|:---:|:---|
| `limit` | `integer` | `200` | Số bản ghi tối đa mỗi lần kéo ($1 \le limit \le 500$, ngoài khoảng sẽ dùng mặc định `200`). |

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "items": [
    {
      "id": "90000000-aaaa-bbbb-cccc-000000000001",
      "serviceName": "user-service",
      "traceId": "00-abc123...-01",
      "userId": "aaaaaaaa-1111-1111-1111-111111111111",
      "userName": "admin@nexus.local",
      "action": "Update",
      "entityName": "User",
      "entityId": "dddddddd-4444-4444-4444-444444444444",
      "oldValues": "{\"Status\":\"Active\"}",
      "newValues": "{\"Status\":\"Banned\"}",
      "changedColumns": "[\"Status\"]",
      "ipAddress": "127.0.0.1",
      "userAgent": "Mozilla/5.0...",
      "createdAt": "2026-11-20T08:00:00Z"
    }
  ],
  "unsyncedCount": 1
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
- [x] **2. Tầng Api:** `InternalAuditLogsController.cs` (`GET /api/v1/internal/audit-logs/unsynced`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test User.sln -c Release` vượt qua 100%.
