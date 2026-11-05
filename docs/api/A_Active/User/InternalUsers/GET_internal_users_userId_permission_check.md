# USER SERVICE - GET /api/v1/internal/users/{userId}/permission-check

> **Feature / Use Case:** InternalUsers — CheckUserActionPermission
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-05
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Cổng kiểm tra quyền hạn nội bộ (Gatekeeper) phục vụ giao tiếp liên service (Internal Inter-Service Communication). Cho phép API Gateway và các service khác (Order, Auction, Catalog) kiểm tra thời gian thực xem người dùng có được phép thực hiện hành động cụ thể hay không dựa trên các án phạt đang hiệu lực.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/internal/users/{userId}/permission-check`
- **Quyền hạn (RBAC Policy):** `Internal` (Gọi từ API Gateway hoặc các downstream microservice nội bộ).
- **Quy tắc Sở hữu (Resource Ownership):** Không yêu cầu token người dùng cá nhân (dành cho hạ tầng liên service).
- **Controller:** `User.Api/Controllers/Internal/InternalUsersController.cs`
- **Application Contract:** `User.Application/Services/Admin/IAdminPenaltyService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `userId` | Route | `Guid` | Có | Định dạng UUIDv4 hợp lệ | Định danh người dùng cần thẩm định quyền |

### 2.2 Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `action` | Query | `string` | Có | Chuỗi không được để trống | Hành vi muốn thực hiện (ví dụ: `Sell`, `Bid`, `Buy`, `Login`, `Comment`) |

### 2.3 Request Body Record DTO
*Endpoint này không sử dụng Request Body.*

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** UserService CHỈ trả về DTO thuần, KHÔNG bọc qua `ApiResponse<T>`.

- **Vị trí file:** `User.Application/Services/Admin/AdminPenaltyDtos.cs`
- **Tên C# Record:** `UserActionPermissionCheckResponseDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record UserActionPermissionCheckResponseDto(
    string UserId,
    string Action,
    bool IsAllowed,
    string? Reason,
    DateTimeOffset? ExpiresAt,
    List<string> ActiveRestrictions
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "userId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "action": "Sell",
  "isAllowed": false,
  "reason": "Seller privileges suspended due to: Repeated order cancellation violations",
  "expiresAt": "2026-11-19T08:00:00Z",
  "activeRestrictions": [
    "Restriction",
    "SuspendSeller"
  ]
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Action parameter is required.` | Thiếu query param `action` |
| `404 Not Found` | `USER_NOT_FOUND` | `User account not found with the specified identifier.` | Người dùng không tồn tại trong hệ thống |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống cơ sở dữ liệu không mong muốn |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Xác thực sự tồn tại của `userId` và trạng thái tài khoản tổng thể (`UserStatus`). Nếu tài khoản `Banned`, lập tức từ chối mọi thao tác (`isAllowed: false`).
2. **Bước 2:** Truy vấn bảng `UserPenalties` lọc các án phạt `Status == PenaltyStatus.Active` và `ExpiresAt > Now` (hoặc vĩnh viễn).
3. **Bước 3:** Đối chiếu hành động `action`:
   - Nếu `action == "Sell"`: Kiểm tra xem có án phạt `TargetRole == TargetRole.Seller` hay không hoặc trạng thái `SellerProfile.Status == Suspended`.
   - Nếu `action == "Bid"`: Kiểm tra xem có án phạt `TargetRole == TargetRole.Buyer` với vi phạm đấu giá hay không.
4. **Bước 4:** Tạo DTO `UserActionPermissionCheckResponseDto` phản ánh chính xác kết quả `isAllowed`, lý do kèm hạn chót.
5. **Bước 5:** Trả về kết quả HTTP 200 OK.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã đối soát code có sẵn trong UserService để tái sử dụng DTO/Enum/Naming.
- [x] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/A_Active/User/InternalUsers/GET_internal_users_userId_permission_check.md`.
- [x] **Persistence:** Truy vấn tối ưu hóa qua Partial Index `IX_UserPenalties_Active_Evaluation`.
- [x] **Application:** Logic thẩm định quyền đã triển khai trong `AdminPenaltyService.CheckUserActionPermissionAsync`.
- [x] **Api:** Thin Controller nội bộ `InternalUsersController` đã sẵn sàng.
- [x] **Tests:** Đã viết unit tests bao phủ các kịch bản cho phép, cấm bán, và tài khoản bị khóa.
- [x] **Verify:** `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release` vượt qua 100% với 0 lỗi.
