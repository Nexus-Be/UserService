# UserService — Full-Flow Seed Data Reference

> **Service:** UserService (Port `8001`, PostgreSQL `user`)
> **Updated:** 2026-11-21
> **Default Password (all `@nexus.com` seed accounts):** `Password123!`
> **Scope:** Dữ liệu đã được dọn rác và bổ sung đầy đủ vào DB cho mọi flow nghiệp vụ (Auth, Roles/RBAC, Seller Registration/Approval, Shipping Addresses, Reputation, Auction Sanctions, Reports, Penalties, Reviews, AuditLogs, SystemConfigs).

---

## 1. Roles & Permissions (`Roles`, `Permissions`, `RolePermissions`)

| RoleId | RoleCode | RoleName | Assigned Permissions |
| :--- | :---: | :--- | :--- |
| `11111111-1111-1111-1111-111111111111` | `0` | `buyer` | — |
| `22222222-2222-2222-2222-222222222222` | `1` | `seller` | — (inherits `buyer`) |
| `33333333-3333-3333-3333-333333333333` | `2` | `staff` | `PERM_USER_READ`, `PERM_SELLER_APPROVE` |
| `44444444-4444-4444-4444-444444444444` | `3` | `admin` | `PERM_USER_READ`, `PERM_USER_WRITE`, `PERM_SELLER_APPROVE`, `PERM_AUCTION_MANAGE` |

---

## 2. Seed Users (`@nexus.com` — Password: `Password123!`)

| UserId | Email | Username | Role | Status | BuyerScore | SellerScore | Flow / Scenario |
| :--- | :--- | :--- | :--- | :---: | :---: | :---: | :--- |
| `aaaaaaaa-1111-1111-1111-111111111111` | `admin@nexus.com` | `admin` | `admin` | `1` (Active) | `100` | `50` | System Admin |
| `bbbbbbbb-2222-2222-2222-222222222222` | `staff@nexus.com` | `staff` | `staff` | `1` (Active) | `90` | `50` | Support Staff (duyệt seller, xử lý report) |
| `cccccccc-3333-3333-3333-333333333333` | `seller@nexus.com` | `seller` | `seller` | `1` (Active) | `95` | `51` | Official Seller (Approved) |
| `dddddddd-4444-4444-4444-444444444444` | `buyer@nexus.com` | `buyer` | `buyer` | `1` (Active) | `51` | `50` | Standard Buyer (có 2 địa chỉ giao hàng) |
| `55555555-5555-5555-5555-555555555551` | `buyer.vip@nexus.com` | `buyer_vip` | `buyer` | `1` (Active) | `92` | `50` | VIP Buyer (Excellent rep, đủ điều kiện đấu giá) |
| `55555555-5555-5555-5555-555555555552` | `buyer.pending@nexus.com` | `buyer_pending` | `buyer` | `0` (PendingVerification) | `50` | `50` | Buyer chưa xác thực OTP email |
| `55555555-5555-5555-5555-555555555553` | `buyer.banned@nexus.com` | `buyer_banned` | `buyer` | `2` (Banned) | `15` | `50` | Buyer bị khóa vĩnh viễn + Auction Suspended |
| `55555555-5555-5555-5555-555555555554` | `buyer.restricted@nexus.com` | `buyer_restricted` | `buyer` | `1` (Active) | `35` | `50` | Buyer bị hạn chế đấu giá tạm thời (Streak=2) |
| `55555555-5555-5555-5555-555555555555` | `seller.pending@nexus.com` | `seller_pending` | `buyer` | `1` (Active) | `55` | `50` | Buyer đã nộp hồ sơ Seller (chờ duyệt) |
| `55555555-5555-5555-5555-555555555556` | `seller.rejected@nexus.com` | `seller_rejected` | `buyer` | `1` (Active) | `50` | `50` | Buyer bị từ chối hồ sơ Seller |
| `55555555-5555-5555-5555-555555555557` | `seller.suspended@nexus.com` | `seller_suspended` | `seller` | `1` (Active) | `50` | `25` | Seller đang bị đình chỉ shop + Penalty Active |
| `55555555-5555-5555-5555-555555555558` | `seller.vip@nexus.com` | `seller_vip` | `seller` | `1` (Active) | `80` | `96` | VIP Seller (Approved, Excellent SellerScore) |

---

## 3. Seller Profiles (`SellerProfiles`)

| SellerProfileId | UserId | ShopName | Email | Status | Province / District / Ward |
| :--- | :--- | :--- | :--- | :---: | :--- |
| `cccccccc-3333-3333-3333-333333333334` | `cccccccc-3333-3333-3333-333333333333` | `Nexus Official Store` | `seller@nexus.com` | `1` (Approved) | `202` / `1442` / `20101` |
| `66666666-6666-6666-6666-666666666658` | `55555555-5555-5555-5555-555555555558` | `Nexus Luxury Collectibles` | `seller.vip@nexus.com` | `1` (Approved) | `202` / `1442` / `20101` |
| `66666666-6666-6666-6666-666666666655` | `55555555-5555-5555-5555-555555555555` | `TechVault Pending Shop` | `seller.pending@nexus.com` | `0` (Pending) | `202` / `1442` / `20101` |
| `66666666-6666-6666-6666-666666666656` | `55555555-5555-5555-5555-555555555556` | `Unverified Electronics` | `seller.rejected@nexus.com` | `2` (Rejected) | `201` / `1485` / `1A0601` |
| `66666666-6666-6666-6666-666666666657` | `55555555-5555-5555-5555-555555555557` | `Suspended Vintage Hub` | `seller.suspended@nexus.com` | `3` (Suspended) | `201` / `1485` / `1A0601` |

---

## 4. Shipping Addresses (`UserAddresses` — dùng cho Commerce / Fulfillment)

| AddressId | UserId | Label | Recipient | Province / District / Ward | AddressLine | IsDefault |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: |
| `77777777-7777-7777-7777-777777777701` | `dddddddd-4444-4444-4444-444444444444` | `Home` | `John Buyer` | `201` / `1485` / `1A0601` | `123 Nguyen Trai` | `true` |
| `77777777-7777-7777-7777-777777777702` | `dddddddd-4444-4444-4444-444444444444` | `Office` | `John Buyer (Office)` | `201` / `1485` / `1A0602` | `18 Duy Tan` | `false` |
| `77777777-7777-7777-7777-777777777703` | `55555555-5555-5555-5555-555555555551` | `Home` | `Minh Nguyen` | `202` / `1442` / `20101` | `45 Le Loi` | `true` |
| `77777777-7777-7777-7777-777777777704` | `55555555-5555-5555-5555-555555555554` | `Home` | `Tuan Hoang` | `202` / `1444` / `20301` | `200 Cach Mang Thang 8` | `true` |
| `77777777-7777-7777-7777-777777777705` | `cccccccc-3333-3333-3333-333333333333` | `Warehouse` | `Nexus Official Warehouse` | `202` / `1442` / `20101` | `Toa nha Nexus, Duy Tan` | `true` |
| `77777777-7777-7777-7777-777777777706` | `55555555-5555-5555-5555-555555555558` | `Boutique` | `Nexus Luxury Collectibles` | `202` / `1442` / `20101` | `102 Dong Khoi` | `true` |

---

## 5. Auction & Sanctions Flow (`UserAuctionProfiles`, `AuctionViolations`, `UserAuctionPayments`)

| UserId | Email | ViolationStreak | RestrictionType | Note |
| :--- | :--- | :---: | :---: | :--- |
| `55555555-5555-5555-5555-555555555551` | `buyer.vip@nexus.com` | `0` | `0` (None) | Có `UserAuctionPayment` (`ORD-...0001`, đúng hạn) |
| `55555555-5555-5555-5555-555555555554` | `buyer.restricted@nexus.com` | `2` | `1` (TempRestricted) | Có `AuctionViolation` (`AUC-...0007`, forfeited `500,000`, hạn đến `2026-11-28`) |
| `55555555-5555-5555-5555-555555555553` | `buyer.banned@nexus.com` | `4` | `2` (Suspended) | Bị đình chỉ đấu giá vĩnh viễn |

---

## 6. Reports, Penalties & Reviews Flow

- **`UserReports` (4 mẫu mới đầy đủ trạng thái)**:
  - `99999444-...-0001`: `Pending (0)` — `buyer.vip` tố cáo `seller.pending` (Spam).
  - `99999444-...-0002`: `UnderReview (1)` — `buyer` tố cáo `seller.suspended` (Counterfeit).
  - `99999444-...-0003`: `Resolved (2)` — `buyer.vip` tố cáo `buyer.banned` (Fraud) → liên kết tới Penalty `99999555-...-0001`.
  - `99999444-...-0004`: `Rejected (3)` — `buyer` tố cáo `seller.vip` (Dismissed by `staff`).
- **`UserPenalties` (3 mẫu mới)**:
  - `99999555-...-0001`: `Active (0)` — `BanAccount`, `-35` điểm (`buyer.banned`).
  - `99999555-...-0002`: `Active (0)` — `Restriction`, `-15` điểm (`seller.suspended`, hết hạn `2026-12-16`).
  - `99999555-...-0003`: `Revoked (2)` — `Warning`, `0` điểm (`buyer.restricted`, được `staff` thu hồi).
- **`UserReviews` (3 mẫu mới)**:
  - `99999666-...-0001`: `buyer.vip` → `seller.vip` (5 sao, `+2` điểm, hiển thị).
  - `99999666-...-0002`: `seller.vip` → `buyer.vip` (5 sao, `+2` điểm, hiển thị).
  - `99999666-...-0003`: `buyer` → `seller.suspended` (1 sao, `IsDisabled = true`, bị `admin` ẩn).

---

## 7. System Configs (`SystemConfigs` — 9 keys)

| ConfigKey | ConfigValue | DataType |
| :--- | :---: | :--- |
| `DEFAULT_REPUTATION_SCORE` | `50` | `Int` |
| `AUTO_APPROVE_BUYER` | `true` | `Boolean` |
| `AUCTION_MIN_REPUTATION_BID` | `50` | `Int` |
| `AUCTION_MIN_REPUTATION_CREATE` | `50` | `Int` |
| `FEATURE_REPUTATION_ENFORCEMENT` | `true` | `Boolean` |
| `PAYMENT_TIMEOUT_PENALTY_SCORE` | `10` | `Int` |
| `SELLER_CANCEL_PENALTY_SCORE` | `5` | `Int` |
| `ANTI_FARMING_COOLDOWN_DAYS` | `7` | `Int` |
| `ORDER_COMPLETED_REWARD_SCORE` | `1` | `Int` |
