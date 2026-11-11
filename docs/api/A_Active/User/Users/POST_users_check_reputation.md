# USER SERVICE - POST /api/v1/users/check-reputation

> **Feature / Use Case:** Users — CheckSellerReputation (INT-04 Dual-Route)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-10
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Checks whether a seller is approved, active, and holds sufficient seller reputation score (>= 50) to create/open an auction.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/users/check-reputation` (Dual-route alias: `/api/v1/internal/users/check-reputation`)
- **Quyền hạn (RBAC Policy):** `AllowAnonymous` / Inter-Service
- **Quy tắc Sở hữu (Resource Ownership):** Evaluates seller eligibility based on requested `userId`.
- **Controller:** `User.Api/Controllers/Public/UsersController.cs` & `User.Api/Controllers/Internal/InternalUsersController.cs`
- **Application Contract:** `User.Application/Services/Internal/IInternalUserService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)

### 2.1 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Internal/InternalUserRequests.cs`
- **Tên C# Record:** `CheckReputationRequest`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed record CheckReputationRequest
{
    [Required(ErrorMessage = "User ID is required.")]
    public Guid UserId { get; init; }

    public string? Action { get; init; } = "SELL";

    public string? RequiredAction { get; init; }

    public string EffectiveAction => !string.IsNullOrWhiteSpace(Action) ? Action : (RequiredAction ?? "SELL");
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "action": "SELL",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

---

## 3. Đầu ra & Hợp đồng DTO (Response Contract)
- **Vị trí file:** `User.Application/Services/Internal/InternalUserResponses.cs`
- **Tên C# Record:** `CheckReputationResponse`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record CheckReputationResponse
{
    public Guid UserId { get; init; }
    public bool Allowed { get; init; }
    public bool IsEligible { get => Allowed; init => Allowed = value; }
    public bool IsApprovedSeller { get; init; }
    public string? UserStatus { get; init; }
    public int CurrentScore { get; init; }
    public string TrustLevel { get; init; } = string.Empty;
    public string? FailureReason { get; init; }
}
```
- **JSON Response Body mẫu (Thành công - Eligible):**
```json
{
  "data": {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "allowed": true,
    "isApprovedSeller": true,
    "userStatus": "Active",
    "currentScore": 80,
    "trustLevel": "Good",
    "failureReason": null
  }
}
```
- **JSON Response Body mẫu (Không đủ điều kiện - Seller Chưa Duyệt):**
```json
{
  "data": {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "allowed": false,
    "isApprovedSeller": false,
    "userStatus": "Active",
    "currentScore": 50,
    "trustLevel": "Standard",
    "failureReason": "SELLER_NOT_APPROVED"
  }
}
```
