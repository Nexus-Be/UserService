# DANH MỤC REST NỘI BỘ SERVICE KHÁC GỌI VÀO USER SERVICE (INBOUND REST)

> **Phân hệ thụ lý:** `UserService`  
> **Base URL:** `http://user-service:8001/api/v1/internal/*`  
> **Controller:** `User.Api/Controllers/Internal/InternalUsersController.cs`  
> **Service Implementation:** `User.Application/Services/Internal/InternalUserService.cs`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `VERIFIED (A_Active)`  

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


---

### 6. `[POST] /api/v1/internal/users/reputation-penalty-revoke`
- **Service gọi:** Order/Commerce Service
- **Mô tả:** Thu hồi án phạt từ chối nhận hàng (`RefusalPenalty`): hoàn lại đúng số điểm đã trừ (tối đa 1 lần cho mỗi sub-order) và đánh dấu chặn án phạt đến trễ.
- **Request (`InternalPenaltyRevokeRequest`):**
  ```json
  {
    "userId": "dddddddd-4444-4444-4444-444444444444",
    "targetRole": "Buyer",
    "referenceId": "SUB-ORD-101",
    "approvedBy": "aaaaaaaa-1111-1111-1111-111111111111",
    "note": "Delivery carrier confirmed address misrouting."
  }
  ```
- **Response 200 OK (`InternalPenaltyRevokeResponse`):**
  ```json
  {
    "userId": "dddddddd-4444-4444-4444-444444444444",
    "targetRole": "Buyer",
    "isDuplicate": false,
    "outcome": "Restored",
    "restoredPoints": 10,
    "previousScore": 40,
    "newScore": 50,
    "newTrustLevel": "Standard"
  }
  ```

### 7. `[POST] /api/v1/internal/users/{userId}/auction-payments`
- **Service gọi:** Auction Service / Payment Service
- **Mô tả:** Ghi nhận thanh toán đơn đấu giá thành công để cập nhật `ViolationStreak` và gỡ bỏ hạn chế đấu giá (nếu đủ điều kiện).
- **Header bắt buộc:** `X-Internal-Service-Key`
- **Request (`RecordAuctionPaymentRequest`):**
  ```json
  {
    "auctionId": "11111111-aaaa-bbbb-cccc-000000000001",
    "orderId": "22222222-aaaa-bbbb-cccc-000000000002",
    "source": "Wallet",
    "paidAt": "2026-11-20T10:00:00Z"
  }
  ```
- **Response (`201 Created` nếu mới, `200 OK` nếu đã ghi nhận):**
  ```json
  {
    "recorded": true,
    "created": true,
    "previousStreak": 1,
    "newStreak": 0,
    "restrictionLifted": true
  }
  ```

### 8. `[GET] /api/v1/internal/users/{userId}/shop`
- **Service gọi:** Catalog Service, Order/Commerce Service, Fulfillment Service
- **Mô tả:** Lấy thông tin gian hàng (`SellerProfile`) và địa chỉ kho GHN của một người bán theo `userId`. Trả `404` nếu chưa có shop.
- **Header bắt buộc:** `X-Internal-Service-Key`
- **Response 200 OK (`InternalShopResponse`):**
  ```json
  {
    "shopId": "22222222-aaaa-bbbb-cccc-000000000001",
    "userId": "cccccccc-3333-3333-3333-333333333333",
    "shopName": "Nexus Audio Store",
    "status": "Approved",
    "pickupAddress": "123 Le Loi, Phuong Ben Nghe, Quan 1, TP.HCM",
    "provinceId": 202,
    "districtId": 1442,
    "wardCode": "20101",
    "hotline": "0901234567"
  }
  ```

### 9. `[POST] /api/v1/internal/sellers/batch-info`
- **Service gọi:** Catalog Service, Order Service, Auction Service
- **Mô tả:** Truy vấn hàng loạt thông tin gian hàng (tối đa 100 `userIds` hoặc `sellerIds`).
- **Header bắt buộc:** `X-Internal-Service-Key`
- **Request (`BatchSellerInfoRequest`):**
  ```json
  {
    "userIds": ["cccccccc-3333-3333-3333-333333333333"],
    "sellerIds": []
  }
  ```
- **Response 200 OK (`IReadOnlyList<InternalSellerBatchItemDto>`):**
  ```json
  [
    {
      "sellerId": "22222222-aaaa-bbbb-cccc-000000000001",
      "userId": "cccccccc-3333-3333-3333-333333333333",
      "shopName": "Nexus Audio Store",
      "avatarUrl": "https://res.cloudinary.com/.../shop.png",
      "status": "Approved",
      "provinceId": 202,
      "districtId": 1442
    }
  ]
  ```

### 10. `[POST] /api/v1/internal/users/contact-info`
- **Service gọi:** Notification Service
- **Mô tả:** Lấy `email` và `fullName` của người dùng phục vụ gửi email thông báo giao dịch. Trả `404` nếu không tìm thấy.
- **Header bắt buộc:** `X-Internal-Service-Key`
- **Request (`InternalContactRequest`):**
  ```json
  {
    "userId": "dddddddd-4444-4444-4444-444444444444"
  }
  ```
- **Response 200 OK (`InternalContactResponse`):**
  ```json
  {
    "userId": "dddddddd-4444-4444-4444-444444444444",
    "email": "buyer@example.com",
    "fullName": "Nguyen Van A"
  }
  ```

### 11. `[GET] /api/v1/internal/users/{userId}`
- **Service gọi:** Order Service, Notification Service, Admin/Audit Service
- **Mô tả:** Lấy thông tin cơ bản nội bộ của một tài khoản người dùng (`email`, `fullName`, `phoneNumber`, `status`, `role`). Trả `404` nếu không tìm thấy.
- **Header bắt buộc:** `X-Internal-Service-Key`
- **Response 200 OK (`InternalUserDetailResponse`):**
  ```json
  {
    "userId": "dddddddd-4444-4444-4444-444444444444",
    "email": "buyer@example.com",
    "fullName": "Nguyen Van A",
    "phoneNumber": "0901234567",
    "status": "Active",
    "role": "BUYER"
  }
  ```

### 12. `[GET] /api/v1/internal/users/{userId}/addresses/default`
- **Service gọi:** Order/Commerce Service, Auction Service
- **Mô tả:** Lấy địa chỉ giao hàng mặc định của người dùng phục vụ checkout và tạo đơn đấu giá. Trả `404` nếu chưa có địa chỉ mặc định.
- **Header bắt buộc:** `X-Internal-Service-Key`
- **Response 200 OK (`InternalUserAddressResponse`):**
  ```json
  {
    "addressId": "11111111-aaaa-bbbb-cccc-000000000001",
    "userId": "dddddddd-4444-4444-4444-444444444444",
    "recipientName": "Nguyen Van A",
    "phone": "0901234567",
    "label": "Home",
    "provinceId": 202,
    "provinceName": "Ho Chi Minh",
    "districtId": 1442,
    "districtName": "Quan 1",
    "wardCode": "20101",
    "wardName": "Phuong Ben Nghe",
    "addressLine": "123 Le Loi",
    "isDefault": true
  }
  ```

### 13. `[GET] /api/v1/internal/users/{userId}/addresses/{addressId}`
- **Service gọi:** Order/Commerce Service
- **Mô tả:** Kiểm tra quyền sở hữu và lấy chi tiết một địa chỉ giao hàng cụ thể của người dùng (phục vụ tính lại phí ship hoặc đổi địa chỉ đơn hàng). Trả `404` nếu không tìm thấy hoặc không thuộc `userId`.
- **Header bắt buộc:** `X-Internal-Service-Key`
- **Response 200 OK (`InternalUserAddressResponse`):** Cấu trúc giống endpoint #12.
