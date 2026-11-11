# DANH MỤC REST NỘI BỘ SERVICE KHÁC GỌI VÀO USER SERVICE (INBOUND REST)

> **Phân hệ thụ lý:** `UserService`  
> **Base URL:** `http://user-service:8001/api/v1/internal/*`  
> **Controller:** `User.Api/Controllers/Internal/InternalUsersController.cs`  
> **Service Implementation:** `User.Application/Services/Internal/InternalUserService.cs`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `✅ VERIFIED (A_Active)`  

---

## Danh Sách Inbound REST Endpoints (`InternalUsersController`)

### 1. `[POST] /api/v1/internal/users/verify-bid-eligibility` (`INT-01`)
> *Dual-Route Alias:* `[POST] /api/v1/users/verify-bid-eligibility`
- **Service gọi:** Auction Service
- **Mô tả:** Thẩm định quyền đặt giá đấu giá của người mua dựa trên quỹ điểm **Buyer Track (`CurrentScore`)**, trạng thái tài khoản `Active` và các án phạt `TargetRole.Buyer` / `TargetRole.Account` đang hiệu lực. Timeout nhanh (<800ms).
- **Request (`VerifyBidEligibilityRequest`):**
  ```json
  {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  }
  ```
- **Response 200 OK:**
  ```json
  {
    "data": {
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "allowed": true,
      "failureReason": null
    }
  }
  ```

### 2. `[POST] /api/v1/internal/users/batch-public-info` (`INT-02`)
> *Dual-Route Alias:* `[POST] /api/v1/users/batch-public-info`
- **Service gọi:** Auction Service, Order/Commerce Service (Tối đa 100 IDs/request)
- **Mô tả:** Truy vấn hàng loạt thông tin hiển thị công khai kèm tên che PII (`MaskedName`), cờ `IsDeleted`, điểm uy tín.
- **Request (`BatchPublicInfoRequest`):**
  ```json
  {
    "userIds": ["3fa85f64-5717-4562-b3fc-2c963f66afa6"]
  }
  ```
- **Response 200 OK:**
  ```json
  {
    "data": [
      {
        "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "maskedName": "T***h",
        "isDeleted": false
      }
    ]
  }
  ```

### 3. `[POST] /api/v1/internal/users/check-reputation` (`INT-04`)
> *Dual-Route Alias:* `[POST] /api/v1/users/check-reputation`
- **Service gọi:** Auction Service
- **Mô tả:** Kiểm tra điều kiện mở phiên đấu giá của người bán dựa trên quỹ điểm độc lập **Seller Track (`SellerScore`)** ($\ge 50$), trạng thái `SellerProfile == Approved` và không có án phạt `TargetRole.Seller` / `TargetRole.Account`.
- **Request (`CheckReputationRequest`):**
  ```json
  {
    "action": "SELL",
    "userId": "cccccccc-3333-3333-3333-333333333333"
  }
  ```
- **Response 200 OK:**
  ```json
  {
    "data": {
      "userId": "cccccccc-3333-3333-3333-333333333333",
      "allowed": true,
      "isApprovedSeller": true,
      "userStatus": "Active",
      "failureReason": null
    }
  }
  ```

### 4. `[POST] /api/v1/internal/users/penalties` (`INT-06`)
- **Service gọi:** Auction Service (bùng thanh toán 24h trừ `10` điểm Buyer), Order Service (Seller hủy đơn trừ `5` điểm Seller)
- **Mô tả:** Trừ điểm uy tín tự động đúng quỹ điểm `TargetRole` (`Buyer` hoặc `Seller`), lưu bản ghi `UserPenalty` và `ReputationLog`. Đảm bảo **Idempotency** theo bộ khóa `(UserId, TargetRole, ReferenceType, ReferenceId, Reason)`.
- **Request (`InternalPenaltyRequest`):**
  ```json
  {
    "userId": "dddddddd-4444-4444-4444-444444444444",
    "targetRole": "Buyer",
    "violationType": "PaymentTimeout",
    "referenceType": "Auction",
    "referenceId": "AUC-TIMEOUT-01",
    "reason": "Buyer failed to pay winning auction invoice within 24 hours."
  }
  ```
- **Response (`InternalPenaltyResponse`):**
  ```json
  {
    "userId": "dddddddd-4444-4444-4444-444444444444",
    "targetRole": "Buyer",
    "isDuplicate": false,
    "penaltyId": "60000000-aaaa-bbbb-cccc-000000000009",
    "previousScore": 50,
    "newScore": 40,
    "appliedDelta": -10,
    "newTrustLevel": "Probation"
  }
  ```

### 5. `[POST] /api/v1/internal/users/reputation-adjust` (`INT-07`)
- **Service gọi:** Order/Commerce Service
- **Mô tả:** Cộng `+1` điểm uy tín cho `Buyer` hoặc `Seller` khi đơn hàng hoàn tất (`OrderCompleted`), có bảo vệ **Idempotency** theo `ReferenceId` và chống cày điểm **7-Day Anti-Farming Cooldown** theo cặp đối tác `(UserId, PartnerUserId, TargetRole)`.
- **Request (`InternalReputationAdjustRequest`):**
  ```json
  {
    "userId": "cccccccc-3333-3333-3333-333333333333",
    "targetRole": "Seller",
    "partnerUserId": "dddddddd-4444-4444-4444-444444444444",
    "scoreDelta": 1,
    "reason": "OrderCompleted",
    "referenceType": "Order",
    "referenceId": "ORD-101"
  }
  ```
- **Response (`InternalReputationAdjustResponse`):**
  ```json
  {
    "userId": "cccccccc-3333-3333-3333-333333333333",
    "targetRole": "Seller",
    "isDuplicate": false,
    "cooldownSuppressed": false,
    "previousScore": 50,
    "newScore": 51,
    "appliedDelta": 1,
    "newTrustLevel": "Standard"
  }
  ```

