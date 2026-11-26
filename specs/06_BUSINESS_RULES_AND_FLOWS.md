# 06 — NGHIỆP VỤ, LUỒNG XỬ LÝ & QUY TẮC BẤT BIẾN (BUSINESS RULES & FLOWS)

> **Mục đích:** Giúp AI Coding Agent và Developer hiểu rõ **luồng nghiệp vụ thực tế**, **máy trạng thái (state machines)** và ranh giới **"cái nào ĐƯỢC LÀM / KHÔNG ĐƯỢC LÀM"** trong `UserService`, tránh sửa code chạy được nhưng sai nghiệp vụ.
> **Nguồn sự thật:** Codebase tại `User.Domain/Constants/`, `User.Domain/Enums/`, `User.Application/Services/`.
> **Cập nhật lần cuối:** 2026-11-26.

---

## 1. PHÂN QUYỀN & PHÂN TÁCH TRÁCH NHIỆM (RBAC & SEGREGATION OF DUTIES - SoD)

Hệ thống chỉ dùng **4 vai trò cố định** (`RoleType`: `Buyer = 0`, `Seller = 1`, `SupportStaff = 2`, `Admin = 3`). Không có Dynamic RBAC.

### Quy tắc Được làm / Không được làm:
| Quy tắc | Hành vi | Mã lỗi nếu vi phạm |
|:---|:---|:---|
| [KHONG] **Không gán `Admin` + `SupportStaff` cùng lúc** | Một tài khoản tuyệt đối không được giữ đồng thời 2 vai trò quản trị & nhân viên (Segregation of Duties). | `SOD_VIOLATION` |
| [KHONG] **Admin không được tạo trực tiếp tài khoản `Seller`** | Tạo user qua `POST /api/v1/admin/users` bị cấm chọn role `Seller`. Seller bắt buộc phải đi qua luồng đăng ký mở shop (`/api/v1/sellers/registration`). | `CANNOT_CREATE_SELLER` |
| [KHONG] **Không gán/tước role `Seller` trực tiếp ở `PUT /roles`** | Không được thêm hoặc gỡ role `Seller` qua `PUT /api/v1/admin/users/{userId}/roles`. Duyệt Seller phải qua Staff (`approve`); đình chỉ Seller phải qua `PUT /api/v1/admin/sellers/{sellerId}/suspend`. | `CANNOT_ASSIGN_SELLER_ROLE_DIRECTLY`, `CANNOT_REVOKE_SELLER_ROLE_DIRECTLY` |
| [KHONG] **Admin không được tự khóa / xóa chính mình** | Admin đang đăng nhập không thể gọi `ban` hoặc `delete` lên chính `userId` của mình. | `CANNOT_BAN_SELF` |
| [KHONG] **Không được khóa / xóa Admin cuối cùng** | Hệ thống luôn phải còn ít nhất 1 `Admin` đang `Active`. | `CANNOT_BAN_LAST_ADMIN`, `CANNOT_DELETE_LAST_ADMIN` |
| **Phân tách duyệt hồ sơ Seller (SoD)** | Duyệt / từ chối hồ sơ đăng ký Seller (`AdminSellerRegistrationsController`) là nghiệp vụ của **Support Staff** (`[RequireRole("staff")]`), tách biệt khỏi quyền quản trị hệ thống của Admin. | `FORBIDDEN` |

---

## 2. LUỒNG XÁC THỰC, MẬT KHẨU & PHIÊN ĐĂNG NHẬP (AUTH & SESSIONS)

* **Trạng thái tài khoản (`UserStatus`):** `PendingVerification (0)` $\rightarrow$ `Active (1)` $\leftrightarrow$ `Banned (2)` / `Deleted (3)`.
* **Đăng ký & OTP (`AuthService`):**
  * Tài khoản mới luôn khởi tạo ở `PendingVerification`, gán role `Buyer`, và tạo `ReputationScore` mặc định **50 điểm** cho cả 2 nhánh (`CurrentScore = 50`, `SellerScore = 50`).
  * Gửi lại OTP có **cooldown 60 giây** (`OTP_COOLDOWN_ACTIVE`, HTTP `429`).
  * Nhập sai OTP **5 lần** trong vòng 5 phút (đăng ký) hoặc 10 phút (quên mật khẩu) $\rightarrow$ **vô hiệu hóa OTP hiện tại** (`OTP_MAX_ATTEMPTS_EXCEEDED`), bắt buộc yêu cầu mã mới.
  * Quên mật khẩu (`POST /api/v1/auth/password/forgot`): **Chống User Enumeration** — luôn trả về `200 OK` với thông báo chung dù email có tồn tại hay không.
* **Đăng nhập & Brute-force:**
  * Sai mật khẩu **5 lần liên tiếp** $\rightarrow$ khóa đăng nhập tạm thời **30 phút** (`ACCOUNT_LOCKED_TEMPORARY`, HTTP `423`).
* **Refresh Token Rotation (RTR):**
  * Mỗi lần gọi `POST /api/v1/auth/refresh`, refresh token cũ bị thu hồi và cấp token mới.
  * Nếu phát hiện **tái sử dụng (reuse)** token đã bị thu hồi $\rightarrow$ coi là lộ token, thu hồi phiên liên quan.
* **Đổi / Đặt lại mật khẩu & Thu hồi phiên:**
  * Đổi/reset mật khẩu hoặc đổi role hoặc bị Ban/Delete $\rightarrow$ **thu hồi toàn bộ Refresh Token / Session** hiện có.

---

## 3. LUỒNG ĐĂNG KÝ & VÒNG ĐỜI NGƯỜI BÁN (SELLER ONBOARDING & LIFECYCLE)

### 3.1. Luồng chuẩn (Happy Path)
1. **Nộp đơn (`POST /api/v1/sellers/registration`):** Lưu bản nháp vào Redis + gửi OTP tới `businessEmail`.
2. **Xác thực OTP (`POST /api/v1/sellers/otp/verify`):** Tạo bản ghi `SellerProfile` ở trạng thái `Pending (0)`.
3. **Upload chứng từ KYC (`POST /api/v1/sellers/registration/documents`):** Upload CCCD 2 mặt / GPKD.
4. **Staff thẩm định:**
   * **Duyệt (`PUT .../approve`):** `SellerProfile.Status` $\rightarrow$ `Approved (1)`, thêm role `Seller`, gọi đồng bộ `FulfillmentService` (`POST /api/v1/internal/warehouses`) để tạo kho GHN, gửi email thông báo. Nếu gọi FulfillmentService lỗi, shop vẫn duyệt (`warehouseReady = false`) và có thể bấm `POST /api/v1/admin/sellers/{sellerId}/warehouse/retry` sau.
   * **Từ chối (`PUT .../reject`):** `SellerProfile.Status` $\rightarrow$ `Rejected (2)` kèm `rejectReason`, gửi email thông báo.
5. **Đình chỉ / Mở lại Shop:**
   * **Suspend (`PUT /api/v1/admin/sellers/{sellerId}/suspend`):** Chuyển `Approved` $\rightarrow$ `Suspended (3)`. **Không khóa quyền mua hàng (Buyer)** của tài khoản đó.
   * **Reactivate (`PUT /api/v1/admin/sellers/{sellerId}/reactivate`):** Chuyển `Suspended` $\rightarrow$ `Approved`.

### 3.2. Các điều cấm khi đăng ký / vận hành Seller
| Điều kiện chặn | Mã lỗi |
|:---|:---|
| Tài khoản chưa `Active` hoặc đang `Banned` | `USER_NOT_ACTIVE` / `USER_BANNED` |
| Tài khoản đã là Seller hoặc đang có đơn `Pending` | `ALREADY_SELLER` / `REQUEST_ALREADY_PENDING` |
| Shop đang bị `Suspended` muốn đăng ký lại | `SELLER_SUSPENDED` (`403`) |
| Điểm uy tín hiện tại `< 50` | `REPUTATION_TOO_LOW` |
| Tên shop chứa từ khóa cấm hoặc đã có người dùng | `VAL_BANNED_SHOP_NAME` / `SHOP_NAME_TAKEN` |
| Số CCCD đã dùng cho shop khác | `IDENTITY_CARD_ALREADY_USED` |
| Sửa chứng từ KYC sau khi đã `Approved` | `KYC_LOCKED` (`409`) |
| Mở lại shop khi tài khoản chủ shop vẫn đang `Banned` | `CANNOT_REACTIVATE_BANNED_USER_SHOP` |
| Thử tạo lại kho (`warehouse/retry`) cho shop chưa `Approved` | `SELLER_NOT_APPROVED` (`409`) |
| Khi Admin Ban user có Shop `Approved` $\rightarrow$ **Tự động Suspend Shop**. Khi Unban user $\rightarrow$ **Chỉ tự mở lại Shop nếu không còn án phạt Seller độc lập nào đang `Active`**. | *(Tự động trong `AdminUserService`)* |

---

## 4. HỆ THỐNG ĐIỂM UY TÍN ĐỘC LẬP (DUAL-TRACK REPUTATION)

UserService tách biệt **2 quỹ điểm độc lập** trong `ReputationScores` (`User.Domain/Constants/ReputationRules.cs`):
* **Buyer Track (`CurrentScore` / `TrustLevel`):** Dùng để kiểm tra quyền đặt giá đấu giá & mua hàng.
* **Seller Track (`SellerScore` / `SellerTrustLevel`):** Dùng để kiểm tra quyền tạo phiên đấu giá & đăng bán.

### 4.1. Thang điểm & Ngưỡng (`0` – `100`, mặc định `50`)
| TrustLevel | Khoảng điểm | Ý nghĩa nghiệp vụ |
|:---|:---:|:---|
| `Untrusted` | `0 – 39` | Bị hạn chế: **Không được đặt giá đấu giá (Bid)** và **Không được mở bán (Sell)**. |
| `Probation` | `40 – 49` | Thử thách: Được phép Bid (ngưỡng mặc định `40`), nhưng **bị chặn tạo phiên đấu giá bán** (ngưỡng mặc định `50`). |
| `Standard` | `50 – 69` | Tiêu chuẩn: Đầy đủ quyền Bid và Sell. |
| `Good` | `70 – 89` | Uy tín tốt. |
| `Excellent` | `90 – 100` | Uy tín xuất sắc. |

*(Lưu ý: Ngưỡng `AUCTION_MIN_REPUTATION_BID` = 40, `AUCTION_MIN_REPUTATION_CREATE` = 50 và `ANTI_FARMING_COOLDOWN_DAYS` = 7 có thể cấu hình động qua `SystemConfigs`.)*

### 4.2. Quy tắc cộng/trừ điểm & Chống cày điểm (Anti-Farming & Idempotency)
* **Hoàn tất đơn hàng (`OrderCompleted`):** `+1` điểm cho Buyer hoặc Seller.
* **Bùng thanh toán đấu giá (`PaymentTimeout`):** `-10` điểm (Buyer Track).
* **Seller hủy đơn (`SellerCancellation`):** `-5` điểm (Seller Track).
* **Đánh giá sau giao dịch (`CreateReview`):**
  * 1 sao: `-2` | 2 sao: `-1` | 3 sao: `0` | 4–5 sao: `+1`.
* **Chống cày điểm 7 ngày (Anti-Farming Cooldown):** Điểm cộng (`+1`) từ **cùng một đối tác (`PartnerUserId`) trên cùng một Track** chỉ được tính tối đa **1 lần mỗi 7 ngày** (cấu hình `ANTI_FARMING_COOLDOWN_DAYS`). Các giao dịch lặp lại trong 7 ngày bị `cooldownSuppressed = true` (`appliedDelta = 0`).
* **Idempotency:** Mọi API trừ/cộng/thu hồi điểm nội bộ (`penalties`, `reputation-adjust`, `reputation-penalty-revoke`) đều bắt buộc idempotent theo `(UserId, TargetRole, ReferenceType, ReferenceId, Reason)` — gọi lặp lại trả kết quả cũ với `isDuplicate = true`, không trừ/cộng đúp.

---

## 5. CHẾ TÀI VI PHẠM ĐẤU GIÁ (AUCTION VIOLATION STREAK)

Định nghĩa tại `User.Domain/Constants/AuctionRules.cs` & `InternalUserService.cs`:
Mỗi lần người thắng đấu giá không thanh toán (`WinnerNonPayment` / `SecondChanceNonPayment`), `ViolationStreak` tăng `+1` và áp dụng chế tài leo thang:

| Streak | Điểm trừ | Chế tài (`AuctionSanctionType`) | Thời hạn |
|:---:|:---:|:---|:---|
| `1` | `-5` | `Warning` | Không giới hạn |
| `2` | `-10` | `DepositRateIncreased` (tăng tỷ lệ cọc) | Không giới hạn |
| `3` | `-15` | `TempRestricted` (cấm đấu giá tạm thời) | **7 ngày** (`TempRestrictionDays = 7`) |
| `>= 4` | `-20` | `Suspended` (đình chỉ quyền đấu giá) | Cho đến khi Admin xử lý |

* Khi thanh toán thành công đơn đấu giá (`POST /api/v1/internal/users/{userId}/auction-payments`), hệ thống cập nhật streak và gỡ hạn chế nếu đủ điều kiện.
* Admin có thể can thiệp streak/hạn chế qua `POST /api/v1/admin/users/{userId}/auction-violations/adjust`.

---

## 6. ĐÁNH GIÁ (REVIEWS) & TỐ CÁO VI PHẠM (REPORTS & REFUNDS)

### 6.1. Đánh giá 2 chiều (`ReviewService`)
* **Thời hạn đánh giá (`ReviewWindowDays`):** Chỉ được đánh giá trong vòng **30 ngày** kể từ khi đơn hàng (`Order` hoặc `Auction`) chuyển trạng thái hoàn tất (`COMPLETED`).
* **Quy tắc:**
  * [KHONG] Không được tự đánh giá chính mình (`SELF_REVIEW_NOT_ALLOWED`).
  * [KHONG] Chỉ bên mua hoặc bên bán của chính đơn hàng đó mới được đánh giá (`REVIEW_NOT_PARTICIPANT`), và mỗi bên chỉ đánh giá **1 lần** cho mỗi `ReferenceId` (`REVIEW_ALREADY_EXISTS`).
  * [KHONG] Buyer đánh giá Seller $\rightarrow$ tác động `SellerScore`. Seller đánh giá Buyer $\rightarrow$ tác động `CurrentScore` (người nhận đánh giá từ Buyer phải có SellerProfile hợp lệ: `INVALID_REVIEW_ROLE`).
* **Ẩn đánh giá vi phạm (`PUT /api/v1/admin/reviews/{reviewId}/hide`):** Bắt buộc nhập lý do tối thiểu 10 ký tự; tự động **bù trừ ngược lại `ScoreImpact`** mà bài đánh giá đó đã cộng/trừ vào điểm uy tín của người bị đánh giá.

### 6.2. Tố cáo & Hoàn tiền (`ReportService`)
* Luồng trạng thái `ReportStatus`: `Pending (0)` / `UnderReview (1)` $\rightarrow$ `Resolved (2)` hoặc `Rejected (3)` (khi Dismiss).
* Khi `Resolve`: có thể áp dụng án phạt (`UserPenalty`) lên đúng `TargetRole` và kích hoạt hoàn tiền qua CommerceService nếu cần.
* Trạng thái hoàn tiền (`ReportRefundStatus`): `None (0)`, `Pending (1)`, `Refunded (2)`, `ManualProcessingRequired (3)`, `Failed (4)`.
  * Chỉ các đơn đã `Resolved` có `RefundStatus` là `Pending` hoặc `Failed` mới được phép bấm `POST /api/v1/admin/reports/{reportId}/refund/retry`.

---

## 7. ĐỊA CHỈ GIAO HÀNG, CẤU HÌNH ĐỘNG & AUDIT LOGS

* **Địa chỉ (`AddressService`):** Người dùng chỉ được xem/sửa/xóa/đặt mặc định địa chỉ thuộc sở hữu của chính mình (`UserId == currentUserId`). Khi đặt 1 địa chỉ làm `IsDefault = true`, các địa chỉ khác của user đó tự động bỏ mặc định.
* **Cấu hình động (`AdminConfigService`):** Chỉ `Admin` được xem/sửa `SystemConfigs` và xem lịch sử thay đổi (`/api/v1/admin/configs/{key}/history`).
* **Audit Logs:** Mọi hành động nhạy cảm (Ban/Unban, AssignRoles, SoftDelete, Approve/Reject/Suspend Seller, AdjustReputation, HideReview, RevokePenalty, UpdateConfig, RemoveAvatar) đều phải ghi qua `_dbContext.AddAuditLog(...)` với `IsSynced = false` để Central Log Service kéo về qua `/api/v1/internal/audit-logs/unsynced`.
