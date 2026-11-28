# USER SERVICE - GET /api/v1/sellers/status

> **Feature / Use Case:** Seller — GetSellerRegistrationStatus
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Retrieves onboarding registration status of current user.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/sellers/status`
- **Quyền hạn (RBAC Policy):** `RequireRole (Authenticated)`
- **Quy tắc Sở hữu (Resource Ownership):** currentUserId
- **Controller:** `User.Api/Controllers/Public/SellerController.cs`
- **Application Contract:** `User.Application/Services/Seller/ISellerService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.3 Request Body Record DTO
- **Vị trí file:** `None (Header Bearer Token)`
- **Tên C# Record:** `None`
- **Khai báo C# & DataAnnotations:**
```csharp
// Identity extracted via HttpContext.GetUserId()
```
- **JSON Request Body mẫu (camelCase):**
```json
{}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `User.Application/Services/Seller/SellerResponses.cs`
- **Tên C# Record:** `SellerRegistrationStatusResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record SellerRegistrationStatusResponseDto(
    string Status,
    string? RejectionReason = null,
    string? NextStep = null,
    string? BusinessEmail = null
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "status": "Pending",
  "rejectionReason": null,
  "nextStep": "WaitForAdminApproval",
  "businessEmail": "contact@techzone.vn"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required.` | Missing bearer token |
| `404 Not Found` | `USER_NOT_FOUND` | `User account not found.` | User does not exist in DB |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `SellerProfileStatus.Pending` | `0` | "Pending" | Under review |
| `SellerProfileStatus.Approved` | `1` | "Approved" | Approved |
| `SellerProfileStatus.Rejected` | `2` | "Rejected" | Rejected |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Extract currentUserId.
2. **Bước 2:** Query latest SellerRegistration record for the user.
3. **Bước 3:** Determine status (None, Draft, Pending, Approved, Rejected).
4. **Bước 4:** Return SellerRegistrationStatusResponseDto with 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/Seller/GET_sellers_status.md`.
- [x] **Persistence:** Entity & EF Core Configuration đã hoàn thiện.
- [x] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/Seller/`.
- [x] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [x] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
