# USER SERVICE - POST /api/v1/users/batch-public-info

> **Feature / Use Case:** Users — GetBatchPublicInfo (INT-02 Dual-Route)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-10
> **Tác giả:** Hiển Vinh & AI Agent
> **Mô tả chức năng:** Fetches batch public profile information with masked display names (e.g. N***n) and soft-delete flags for up to 100 user IDs.

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/users/batch-public-info` (Dual-route alias: `/api/v1/internal/users/batch-public-info`)
- **Quyền hạn (RBAC Policy):** `AllowAnonymous` / Inter-Service
- **Quy tắc Sở hữu (Resource Ownership):** Publicly safe masked user names.
- **Controller:** `User.Api/Controllers/Public/UsersController.cs` & `User.Api/Controllers/Internal/InternalUsersController.cs`
- **Application Contract:** `User.Application/Services/Internal/IInternalUserService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)

### 2.1 Request Body Record DTO
- **Vị trí file:** `User.Application/Services/Internal/InternalUserRequests.cs`
- **Tên C# Record:** `BatchPublicInfoRequest`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed record BatchPublicInfoRequest
{
    [Required(ErrorMessage = "User IDs list is required.")]
    [MinLength(1, ErrorMessage = "User IDs list must contain at least 1 identifier.")]
    [MaxLength(100, ErrorMessage = "User IDs list cannot exceed 100 identifiers per batch request.")]
    public IReadOnlyList<Guid> UserIds { get; init; } = Array.Empty<Guid>();
}
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "userIds": [
    "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "7ca34b12-4211-4712-a1bb-1d883f55bcb2"
  ]
}
```

---

## 3. Đầu ra & Hợp đồng DTO (Response Contract)
- **Vị trí file:** `User.Application/Services/Internal/InternalUserResponses.cs`
- **Tên C# Record:** `UserPublicInfoItem`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record UserPublicInfoItem
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string MaskedDisplayName { get; init; } = string.Empty;
    public string MaskedName { get; init; } = string.Empty;
    public bool IsDeleted { get; init; }
    public string? AvatarUrl { get; init; }
    public int ReputationScore { get; init; }
    public int SellerReputationScore { get; init; }
}
```
- **JSON Response Body mẫu (Thành công - JSON Array in data):**
```json
{
  "data": [
    {
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "maskedName": "N***n",
      "isDeleted": false
    },
    {
      "userId": "7ca34b12-4211-4712-a1bb-1d883f55bcb2",
      "maskedName": "J***e",
      "isDeleted": false
    }
  ]
}
```
