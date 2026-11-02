# DANH MỤC REST NỘI BỘ SERVICE KHÁC GỌI VÀO USER SERVICE (INBOUND REST)

> **Phân hệ thụ lý:** `UserService`  
> **Base URL:** `http://user-service:8001/api/v1/internal/*`  
> **Trạng thái tài liệu:** `B_none (Chờ hoàn thiện & nghiệm thu liên-service)`  
> **Trạng thái triển khai:** `⏳ IN_PROGRESS`  

---

## Danh Sách 7 Inbound REST Endpoints

1. **`[POST] /api/v1/internal/users/verify-bid-eligibility`** (`INT-01`)
   - **Service gọi:** Auction Service (Hot Path $\le 15$ms qua Redis `user:rep:{id}`)
   - **Mô tả:** Thẩm định quyền đặt giá đấu giá: kiểm tra User trạng thái `ACTIVE` và điểm uy tín $\ge 40$. Body: `{ "userId": "uuid" }`.

2. **`[POST] /api/v1/internal/users/batch-public-info`** (`INT-02`)
   - **Service gọi:** Auction Service, Commerce Service (Max 100 IDs)
   - **Mô tả:** Lấy danh sách tóm tắt hàng loạt kèm tên che PII (`maskedName: "Ng***** A"`, `avatarUrl`) cho lịch sử đấu giá / đơn hàng.

3. **`[GET] /api/v1/internal/sellers/{sellerId}/pickup-address`** (`INT-03`)
   - **Service gọi:** Fulfillment Service
   - **Mô tả:** Lấy địa chỉ kho lấy hàng GHN chuẩn 4 cấp hành chính của Shop phục vụ tính phí ship và lập vận đơn.

4. **`[POST] /api/v1/internal/users/check-reputation`** (`INT-04`)
   - **Service gọi:** Auction Service
   - **Mô tả:** Kiểm tra điều kiện mở phiên đấu giá của Seller (Yêu cầu `isApprovedSeller = true`, `ACTIVE`, điểm uy tín $\ge 50$). Body: `{ "userId": "uuid" }`.

5. **`[GET] /api/v1/internal/users/{id}/contact`** (`INT-05`)
   - **Service gọi:** Notification Service
   - **Mô tả:** Cung cấp thông tin họ tên, email, SĐT của User để gửi thông báo giao dịch.

6. **`[POST] /api/v1/internal/users/penalties`** (`INT-06`)
   - **Service gọi:** Auction Service (bùng kèo 24h trừ 10đ), Commerce Service (Shop hủy đơn trừ 5đ)
   - **Mô tả:** Nhận yêu cầu phạt trừ điểm uy tín, lưu chính thức vào bảng `UserPenalties` và `ReputationLogs`. Có kiểm tra Idempotency theo `referenceId`.

7. **`[POST] /api/v1/internal/users/reputation-adjust`** (`INT-07`)
   - **Service gọi:** Commerce Service
   - **Mô tả:** Thưởng +1 điểm uy tín cho Buyer & Seller khi đơn hàng hoàn tất (`COMPLETED`).
