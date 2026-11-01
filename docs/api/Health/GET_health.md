# USER SERVICE - GET /health

> **Feature / Use Case:** Health — HealthCheck
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** AI Assistant (UserService Architecture Team)
> **Mô tả chức năng:** Performs service health verification for container orchestration and API gateway.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/health`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous`
- **Quy tắc Sở hữu (Resource Ownership):** Public health probe
- **Controller:** `User.Api/Program.cs (Minimal API)`
- **Application Contract:** `In-process health check probe`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `None`
- **Tên C# Record:** `None`
- **Khai báo C# & DataAnnotations:**
```csharp
// No parameters
```
- **JSON Request Body mẫu (camelCase):**
```json
{}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `Anonymous Object`
- **Tên C# Record:** `{ status: string, service: string }`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record HealthResponse(string Status, string Service);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "status": "Healthy",
  "service": "user-service"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Database or critical service dependency is down |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Probe core application readiness.
2. **Bước 2:** Return 200 OK with status "Healthy" and service identifier.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Health/GET_health.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Health/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
