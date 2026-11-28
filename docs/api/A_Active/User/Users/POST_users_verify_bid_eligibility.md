# USER SERVICE - POST /api/v1/users/verify-bid-eligibility

> **Feature / Use Case:** Users — VerifyBidEligibility (INT-01 Dual-Route)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-10
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Verifies whether a buyer is active, unbanned, and holds sufficient buyer score to place a bid (<800ms SLA).

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/users/verify-bid-eligibility` (Dual-route alias: `/api/v1/internal/users/verify-bid-eligibility`)
- **Quyền hạn (RBAC Policy):** `AllowAnonymous` / Inter-Service
- **Quy tắc Sở hữu (Resource Ownership):** Evaluates buyer bidding eligibility based on requested `userId`.
- **Controller:** `User.Api/Controllers/Public/UsersController.cs` & `User.Api/Controllers/Internal/InternalUsersController.cs`
- **Application Contract:** `User.Application/Services/Internal/IInternalUserService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)

### 2.1 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Internal/InternalUserRequests.cs`
- **Tên C# Record:** `VerifyBidEligibilityRequest`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed record VerifyBidEligibilityRequest
{
    [Required(ErrorMessage = "User ID is required.")]
    public Guid UserId { get; init; }

    public Guid? AuctionId { get; init; }
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

---

## 3. Đầu ra & Hợp đồng DTO (Response Contract)
- **Vị trí file:** `User.Application/Services/Internal/InternalUserResponses.cs`
- **Tên C# Record:** `VerifyBidEligibilityResponse`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record VerifyBidEligibilityResponse
{
    public Guid UserId { get; init; }
    public bool Allowed { get; init; }
    public bool IsEligible { get => Allowed; init => Allowed = value; }
    public int CurrentScore { get; init; }
    public string? FailureReason { get; init; }
}
```
- **JSON Response Body mẫu (Thành công - Eligible):**
```json
{
  "data": {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "allowed": true,
    "failureReason": null
  }
}
```
