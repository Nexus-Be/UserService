---
title: "Checklist Toàn Bộ API User Service - 100% RESTful (Chuẩn Hóa Plural Routing & Specs Catalog)"
role: "User Service Lead"
author: "Trần Hoàng Vinh (vinhth)"
project: "Project Nexus"
version: "3.3 (Aligned with UserService Docs Catalog v3.3 & PR #40 Reputation/Governance)"
date: "2026-11-10"
architecture:
  protocol: "100% RESTful Web API (HTTP/JSON)"
  routing: "100% Plural Route Convention (/api/v1/users, /api/v1/admin/users, /api/v1/sellers, /api/v1/admin/sellers, /api/v1/admin/penalties, /api/v1/me, /api/v1/auth, /api/v1/internal/users)"
  authorization: "4 Fixed Roles (ADMIN, STAFF, SELLER, BUYER) - No Dynamic RBAC"
  inter_service: "Direct Synchronous REST (Internal Tokens) - No gRPC, No RabbitMQ"
  specs_catalog: "UserService/docs/api/"
tags:
  - user-service
  - api-checklist
  - restful-only
  - plural-routing
  - no-grpc
  - no-rabbitmq
  - no-rbac
---

# 📋 CHECKLIST TOÀN BỘ API PHÂN HỆ USER SERVICE (PROJECT NEXUS)
### ⚡ Chuẩn Hóa Kiến Trúc: 100% RESTful Web API | Plural Route Convention | 4 Roles Cố Định | Đã Loại Bỏ Hoàn Toàn gRPC, RabbitMQ & Dynamic RBAC

> **Dành cho:** Lead Phân hệ User Service (Trần Hoàng Vinh)  
> **Cổng Microservice:** `user-service:8001` | **Database:** `usermanagement_db:5433` | **API Gateway YARP:** `8080`  
> **Nguồn tài liệu đặc tả chuẩn:** `UserService/docs/api/` (71 Web & Inbound APIs trong `A_Active` đã kiểm chứng 432/432 Tests Pass)  
> **Tuyên ngôn kiến trúc chuẩn:**
> 1. **100% RESTful Web API & Plural Routes:** Toàn bộ giao tiếp đều dùng HTTP/JSON với tiền tố số nhiều chuẩn REST (`/api/v1/users`, `/api/v1/admin/users`, `/api/v1/sellers`, `/api/v1/admin/sellers`, `/api/v1/admin/penalties`, `/api/v1/me`, `/api/v1/auth`, `/api/v1/internal/users`).
> 2. **Bỏ hoàn toàn Dynamic RBAC & Message Broker:** Đã xóa sạch gRPC nhị phân và RabbitMQ. Phân quyền cố định 4 Roles: `ADMIN`, `STAFF`, `SELLER`, `BUYER` theo nguyên tắc Strict 1 User - 1 Primary Role & Segregation of Duties (SoD).
> 3. **Hệ thống Điểm Uy Tín Độc Lập Dual-Track (PR #40):** Tách biệt rạch ròi điểm uy tín Người Mua (Buyer Track - `CurrentScore`) và Người Bán (Seller Track - `SellerScore`). Chống cày điểm 7-Day Cooldown và bảo vệ Idempotency nghiêm ngặt.
> 4. **Quy ước trạng thái:**
>    - `[x]`: **ĐÃ HOÀN THÀNH & KIỂM CHỨNG** trong Codebase C# (.NET 8 Clean Architecture) và có tài liệu chi tiết tại `docs/api/A_Active/User/`.
>    - `[ ]`: **CHƯA THỰC HIỆN / ĐANG CHỜ** (Bảo lưu theo dõi tại `docs/api/B_none/` thuộc Phase 2 / Liên Service).
>    - `[-]`: **ĐÃ BÃI BỎ / DECOUPLE** (Bỏ kết nối trực tiếp khỏi UserService để giảm phụ thuộc theo kiến trúc mới).

---

## 📊 TIẾN ĐỘ THỰC HIỆN TỔNG QUAN

| Nhóm Nghiệp Vụ | Tổng API | Đã xong `[x]` | Chưa làm `[ ]` | Bãi bỏ `[-]` | Ghi chú kiến trúc & Đối soát Docs |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **1. Xác thực & Quản lý Phiên (Auth Flow)** | 13 | **13** | 0 | 0 | Hoàn thành Register, Login, Refresh, Password, Test, Logout-all & Sessions (`GET/DELETE /me/sessions`). |
| **2. Hồ sơ Cá nhân & Avatar (Profile Flow)** | 13 | **11** | 2 | 0 | Hoàn thành Me Profile, Safe Profile (`/users/{userId}`), Avatar (`POST/GET /me/avatar`, `GET /users/{userId}/avatar`), Uy tín & Policy. Còn Đổi Email |
| **3. Đăng ký & Quản lý Shop (Seller Flow)** | 11 | **11** | 0 | 0 | Hoàn thành Onboarding, OTP, Status, Profile, Public Shop, Upload Docs CCCD, Suspend, Reactivate & Chi tiết Admin. |
| **4. Quản trị User & Kỷ luật (Admin Flow)** | 20 | **19** | 1 | 0 | Hoàn thành CRUD Users, Ban/Unban, Roles SoD, Duyệt/Từ chối Shop, Danh sách/Chi tiết/Thu hồi Án phạt, Chỉnh Uy tín & System Configs. Còn Audit Logs |
| **5. Giám Sát Hoạt Động (Health Check)** | 1 | **1** | 0 | 0 | Hoàn thành Endpoint `/health` phục vụ Docker & Gateway |
| **6. Đánh giá & Tố cáo Vi phạm (Reviews & Reports)** | 10 | **10** | 0 | 0 | Hoàn thành Tạo/Xem/Ẩn Review, Nộp/Xem/Duyệt/Bác bỏ Report & Upload file đính kèm (`FILE-01`, `FILE-02`). |
| **7. REST Nội bộ Service Gọi Đến (Inbound REST)** | 8 | **6** | 2 | 0 | Hoàn thành Gatekeeper permission-check, verify-bid-eligibility, batch-public-info, check-reputation, penalties, reputation-adjust. Còn Warehouse & Contact |
| **8. REST Nội bộ UserService Gọi Đi (Outbound REST)**| 4 | **3** | 0 | **1** | Hoàn thành gọi Notification (8007) & Fulfillment (8006). Đã Decouple bãi bỏ gọi Wallet (8008) theo PR #37 |
| **TỔNG CỘNG USER SERVICE** | **80 API** | **74** | **5** | **1** | **Toàn bộ 71 Web & Inbound APIs trong A_Active Docs đã Verify 100% (432/432 Tests Pass)** |

---

## 🔑 NHÓM 1: XÁC THỰC & QUẢN LÝ PHIÊN (AUTH FLOW - 13 API)

> **Mục tiêu:** Quản lý vòng đời đăng ký, kích hoạt OTP, bảo mật tài khoản, xoay vòng Token RTR và thu hồi phiên.

- [x] **AUTH-01** — `POST /api/v1/auth/register`
  - **Tên Function:** `Register` | **Role:** `AllowAnonymous`
  - **Mô tả:** Đăng ký tài khoản Buyer mới. Tạo User trạng thái `PENDING_ACTIVATION`, khởi tạo điểm uy tín ban đầu là 50.
  - **REST Nội bộ gọi đi:** `POST http://notification-service:8007/api/v1/internal/notifications/email-otp`
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Auth/POST_register.md`
- [x] **AUTH-TEST** — `POST /api/v1/users/auth/test`
  - **Tên Function:** `RegisterTest` | **Role:** `AllowAnonymous`
  - **Mô tả:** Đăng ký tài khoản thử nghiệm nhanh với kích hoạt gửi OTP tức thì phục vụ kiểm thử end-to-end.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Auth/POST_users_auth_test.md`
- [x] **AUTH-02** — `POST /api/v1/auth/otp/verify`
  - **Tên Function:** `VerifyOtp` | **Role:** `AllowAnonymous`
  - **Mô tả:** Xác thực OTP 6 số kích hoạt tài khoản sang trạng thái `ACTIVE`. (Lưu ý: Đã decouple hoàn toàn khỏi Wallet Service theo PR #37, UserService không còn gọi tạo ví).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Auth/POST_otp_verify.md`
- [x] **AUTH-03** — `POST /api/v1/auth/otp/resend`
  - **Tên Function:** `ResendOtp` | **Role:** `AllowAnonymous`
  - **Mô tả:** Gửi lại mã OTP qua email (Cooldown 60s qua Redis `otp:cooldown:{email}`, max 10 lần/ngày).
  - **REST Nội bộ gọi đi:** `POST http://notification-service:8007/api/v1/internal/notifications/email-otp`
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Auth/POST_otp_resend.md`
- [x] **AUTH-04** — `POST /api/v1/auth/login`
  - **Tên Function:** `Login` | **Role:** `AllowAnonymous`
  - **Mô tả:** Đăng nhập hệ thống (Email/Username + Password). Cấp Access Token JWT (60 phút) + Refresh Token (30 ngày). Nhúng vai trò cố định (`ADMIN`, `STAFF`, `SELLER`, `BUYER`). Khóa 30 phút nếu sai 5 lần liên tiếp.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Auth/POST_login.md`
- [x] **AUTH-05** — `POST /api/v1/auth/refresh`
  - **Tên Function:** `RefreshToken` | **Role:** `AllowAnonymous`
  - **Mô tả:** Làm mới phiên ngầm (Refresh Token Rotation - RTR). Thu hồi token cũ, cấp mới, phát hiện reuse token.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Auth/POST_refresh.md`
- [x] **AUTH-06** — `POST /api/v1/auth/password/forgot`
  - **Tên Function:** `ForgotPassword` | **Role:** `AllowAnonymous`
  - **Mô tả:** Yêu cầu quên mật khẩu & gửi OTP khôi phục (Chống User Enumeration: luôn trả về HTTP `200 OK`).
  - **REST Nội bộ gọi đi:** `POST http://notification-service:8007/api/v1/internal/notifications/email-otp` (`type: "FORGOT_PASSWORD"`)
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Auth/POST_password_forgot.md`
- [x] **AUTH-07** — `PUT /api/v1/auth/password/reset`
  - **Tên Function:** `ResetPassword` | **Role:** `AllowAnonymous`
  - **Mô tả:** Đặt lại mật khẩu mới bằng mã OTP. Thu hồi toàn bộ phiên đăng nhập cũ trong `UserSessions`.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Auth/PUT_password_reset.md`
- [x] **AUTH-08** — `PATCH /api/v1/auth/password`
  - **Tên Function:** `ChangePassword` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Người dùng chủ động đổi mật khẩu trong hồ sơ cá nhân khi đang đăng nhập.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Auth/PATCH_password.md`
- [x] **AUTH-09** — `POST /api/v1/auth/logout`
  - **Tên Function:** `Logout` | **Role:** `AllowAnonymous / Authenticated`
  - **Mô tả:** Đăng xuất & thu hồi Refresh Token hiện tại.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Auth/POST_logout.md`
- [x] **USER-05** — `POST /api/v1/auth/logout-all`
  - **Tên Function:** `LogoutAll` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Đăng xuất và vô hiệu hóa toàn bộ các phiên hoạt động trên mọi thiết bị của người dùng.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Auth/POST_logout_all.md`
- [x] **USER-03** — `GET /api/v1/me/sessions`
  - **Tên Function:** `GetActiveSessions` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Xem danh sách các thiết bị, IP, thời gian đang duy trì phiên đăng nhập của mình.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Me/GET_me_sessions.md`
- [x] **USER-04** — `DELETE /api/v1/me/sessions/{id}`
  - **Tên Function:** `RevokeSession` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Thu hồi / Đăng xuất thiết bị từ xa theo Session ID cụ thể.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Me/DELETE_me_sessions_id.md`

---

## 👤 NHÓM 2: HỒ SƠ CÁ NHÂN & AVATAR (PROFILE FLOW - 13 API)

> **Mục tiêu:** Cung cấp thông tin tài khoản, avatar tĩnh, kiểm tra quyền tham gia đấu giá/bán hàng và cập nhật hồ sơ.

- [x] **USER-01** — `GET /api/v1/me`
  - **Tên Function:** `GetCurrentUserProfile` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Xem hồ sơ cá nhân của chính mình (Data Masking: che SĐT `090*****67`, che CCCD `079******123`).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Me/GET_me.md`
- [x] **USER-02** — `PATCH /api/v1/me`
  - **Tên Function:** `UpdateCurrentUserProfile` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Cập nhật thông tin cá nhân (Họ tên, ngày sinh, giới tính, địa chỉ). Chặn sửa `email`, `roles`.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Me/PATCH_me.md`
- [x] **USER-07** — `GET /api/v1/users/{userId}`
  - **Tên Function:** `GetUserPublicProfile` | **Role:** `AllowAnonymous`
  - **Mô tả:** Xem hồ sơ công khai an toàn của người dùng khác (Safe Public Profile, tuyệt đối không trả về SĐT, CCCD, Email).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Users/GET_users_userId.md`
- [x] **USER-08** — `GET /api/v1/me/reputation`
  - **Tên Function:** `GetMyReputation` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Xem chi tiết điểm uy tín độc lập Buyer/Seller (`currentScore`, `sellerScore`, `buyerTrustLevel`, `sellerTrustLevel`) và cờ điều kiện tham gia (`canBid`, `canSell`).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Me/GET_me_reputation.md`
- [x] **POLICY-01** — `GET /api/v1/reputation/policy`
  - **Tên Function:** `GetReputationPolicy` | **Role:** `AllowAnonymous`
  - **Mô tả:** Cung cấp thông số cấu hình ngưỡng uy tín toàn sàn (`minReputationToBid: 40`, `minReputationToCreateAuction: 50`) cho Frontend hiển thị nhãn.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Reputation/GET_reputation_policy.md`
- [x] **USER-09** — `GET /api/v1/me/penalties`
  - **Tên Function:** `GetMyPenalties` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Tra cứu danh sách các biên bản xử phạt trừ điểm uy tín của bản thân (Bùng kèo, shop hủy đơn) có phân trang và phân loại vai trò vi phạm.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Me/GET_me_penalties.md`
- [x] **REVIEW-05** — `GET /api/v1/users/{userId}/reputation`
  - **Tên Function:** `GetUserReputation` | **Role:** `AllowAnonymous`
  - **Mô tả:** Xem Thẻ tóm tắt hồ sơ uy tín công khai của một đối tác (Hiển thị `reputationScore`, `sellerReputationScore` và nhãn cấp bậc tương ứng).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Users/GET_users_userId_reputation.md`
- [x] **REVIEW-03** — `GET /api/v1/users/{userId}/reputation/history`
  - **Tên Function:** `GetUserReputationHistory` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Xem lịch sử biến động điểm uy tín theo thời gian (+/- điểm kèm lý do, vai trò chịu phạt và mã giao dịch tham chiếu).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Users/GET_users_userId_reputation_history.md`
- [x] **USER-06** — `POST /api/v1/me/avatar`
  - **Tên Function:** `UploadMyAvatar` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Tải lên ảnh đại diện cá nhân (Multipart/form-data, Max 2MB, định dạng JPG/PNG/WebP). Lưu trữ Cloudinary (`avatars/{userId}`).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Me/POST_me_avatar.md`
- [x] **USER-06B** — `GET /api/v1/me/avatar`
  - **Tên Function:** `GetMyAvatar` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** API rút gọn lấy nhanh URL avatar phục vụ Header UI.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Me/GET_me_avatar.md`
- [x] **USER-07B** — `GET /api/v1/users/{userId}/avatar`
  - **Tên Function:** `GetUserAvatar` | **Role:** `AllowAnonymous`
  - **Mô tả:** Tải nhanh ảnh avatar công khai của người dùng khác trong danh sách đấu giá / bình luận.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Users/GET_users_userId_avatar.md`
- [ ] **USER-10** — `POST /api/v1/me/email/change-request`
  - **Tên Function:** `RequestEmailChange` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Yêu cầu đổi email đăng nhập & gọi REST nội bộ sang Notification để gửi OTP về email mới.
  - **Tài liệu đặc tả:** `docs/api/B_none/USER-10_POST_ME_EMAIL_CHANGE_REQUEST.md`
- [ ] **USER-11** — `POST /api/v1/me/email/change-verify`
  - **Tên Function:** `VerifyEmailChange` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Xác thực OTP email mới, cập nhật email trong CSDL và thu hồi mọi phiên đăng nhập cũ.
  - **Tài liệu đặc tả:** `docs/api/B_none/USER-11_POST_ME_EMAIL_CHANGE_VERIFY.md`

---

## 🏬 NHÓM 3: HỒ SƠ MỞ GIAN HÀNG & QUẢN LÝ SELLER (SELLER FLOW - 11 API)

> **Mục tiêu:** Quy trình Buyer nộp đơn nâng hạng Shop, xác thực OTP email doanh nghiệp, Staff thẩm định, kích hoạt kho GHN, quản trị gian hàng và chế tài đình chỉ/phục hồi Shop.

- [x] **SELLER-01** — `POST /api/v1/sellers/registration`
  - **Tên Function:** `RegisterSeller` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Buyer nộp hồ sơ xin mở Shop (Tên shop, Business Email, Hotline, CCCD, địa chỉ kho GHN). Tạo hồ sơ `PENDING`.
  - **REST Nội bộ gọi đi:** Gọi `POST http://notification-service:8007/api/v1/internal/notifications/email-otp` gửi OTP tới `businessEmail`.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Seller/POST_sellers_registration.md`
- [x] **SELLER-02** — `POST /api/v1/sellers/otp/resend`
  - **Tên Function:** `ResendSellerOtp` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Gửi lại mã OTP xác thực cho email doanh nghiệp mở Shop.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Seller/POST_sellers_otp_resend.md`
- [x] **SELLER-04** — `POST /api/v1/sellers/otp/verify`
  - **Tên Function:** `VerifySellerOtp` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Xác thực OTP email doanh nghiệp (Chuyển hồ sơ sang `PENDING_APPROVAL` để đưa vào hàng đợi Staff duyệt).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Seller/POST_sellers_otp_verify.md`
- [x] **SELLER-05** — `GET /api/v1/sellers/status`
  - **Tên Function:** `GetSellerRegistrationStatus` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Buyer theo dõi tiến độ xét duyệt đơn mở Shop của mình.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Seller/GET_sellers_status.md`
- [x] **SELLER-06** — `GET /api/v1/sellers/profile`
  - **Tên Function:** `GetSellerOwnProfile` | **Role:** `RequireRole("seller", "admin")`
  - **Mô tả:** Seller tự xem thông tin gian hàng, hotline, địa chỉ kho lấy hàng GHN của mình.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Seller/GET_sellers_profile.md`
- [x] **SELLER-07** — `PATCH /api/v1/sellers/profile`
  - **Tên Function:** `UpdateSellerOwnProfile` | **Role:** `RequireRole("seller", "admin")`
  - **Mô tả:** Seller cập nhật thông tin mô tả shop, hotline, logo gian hàng và kho lấy hàng.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Seller/PATCH_sellers_profile.md`
- [x] **SELLER-08** — `GET /api/v1/sellers/{id}`
  - **Tên Function:** `GetPublicSellerProfile` | **Role:** `AllowAnonymous`
  - **Mô tả:** Khách / Quản trị viên xem hồ sơ công khai của Shop theo Shop ID hoặc Seller User ID.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Seller/GET_sellers_id.md`
- [x] **SELLER-09** — `GET /api/v1/admin/sellers/{sellerId}`
  - **Tên Function:** `GetAdminSellerProfile` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Quản trị viên / Nhân viên xem hồ sơ chi tiết người bán, thông tin KYC, địa chỉ kho lấy hàng GHN và điểm uy tín người bán.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminSellers/GET_admin_sellers_sellerId.md`
- [x] **SELLER-10** — `PUT /api/v1/admin/sellers/{sellerId}/suspend`
  - **Tên Function:** `SuspendSeller` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Đình chỉ hoạt động gian hàng người bán (`ShopStatus = Suspended`). Quyền mua sắm Buyer của tài khoản vẫn giữ nguyên không bị ảnh hưởng.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminSellers/PUT_admin_sellers_sellerId_suspend.md`
- [x] **SELLER-11** — `PUT /api/v1/admin/sellers/{sellerId}/reactivate`
  - **Tên Function:** `ReactivateSeller` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Khôi phục quyền kinh doanh của gian hàng Shop (`ShopStatus = Active`).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminSellers/PUT_admin_sellers_sellerId_reactivate.md`
- [x] **SELLER-03** — `POST /api/v1/sellers/registration/documents`
  - **Tên Function:** `UploadSellerDocuments` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Upload ảnh 2 mặt CCCD và Giấy phép kinh doanh phục vụ thẩm định hồ sơ mở Shop (Cloudinary Signed URL, TTL 10 phút).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Seller/POST_sellers_registration_documents.md`

---

## 🛡️ NHÓM 4: QUẢN TRỊ USER, DUYỆT SHOP & KỶ LUẬT (ADMIN FLOW - 20 API)

> ⚠️ **ĐÃ BÃI BỎ HOÀN TOÀN DYNAMIC RBAC:**
> - Hệ thống chỉ duy trì **4 Roles Cố Định:** `ADMIN`, `STAFF`, `SELLER`, `BUYER` theo nguyên tắc Strict 1 User - 1 Primary Role & Segregation of Duties (SoD).
> - Tuyến đường chuẩn hóa 100% Plural Route Convention (`/api/v1/admin/users`, `/api/v1/admin/roles`, `/api/v1/admin/seller-registrations`, `/api/v1/admin/penalties`, `/api/v1/admin/configs`).

- [x] **ADMIN-01** — `GET /api/v1/admin/users`
  - **Tên Function:** `ListUsers` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Tra cứu danh sách người dùng toàn hệ thống (Phân trang DB, tìm kiếm từ khóa, lọc theo 4 Role và Status).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminUsers/GET_admin_users.md`
- [x] **ADMIN-01B** — `POST /api/v1/admin/users`
  - **Tên Function:** `CreateUser` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Admin tạo tài khoản nhân viên `STAFF` hoặc tài khoản người dùng trực tiếp.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminUsers/POST_admin_users.md`
- [x] **ADMIN-02** — `GET /api/v1/admin/users/{userId}`
  - **Tên Function:** `GetUserDetail` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Xem chi tiết hồ sơ quản trị, trạng thái tài khoản, vai trò và điểm uy tín của 1 User.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminUsers/GET_admin_users_userId.md`
- [x] **ADMIN-03** — `PUT /api/v1/admin/users/{userId}`
  - **Tên Function:** `UpdateUser` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Admin cập nhật thông tin tài khoản người dùng hiện có.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminUsers/PUT_admin_users_userId.md`
- [x] **ADMIN-04** — `DELETE /api/v1/admin/users/{userId}`
  - **Tên Function:** `DeleteUser` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Xóa mềm tài khoản (`isDeleted = true`), thu hồi ngay phiên đăng nhập.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminUsers/DELETE_admin_users_userId.md`
- [x] **ADMIN-05** — `PATCH /api/v1/admin/users/{userId}/ban`
  - **Tên Function:** `BanUser` | **Role:** `RequireRole("admin")`
  - **Mô tả:** Khóa tài khoản vi phạm quy chế (Kèm lý do vi phạm, thu hồi token trên Redis). Hỗ trợ cả alias tương thích ngược `/api/v1/admin/user/{userId}/ban`.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminUsers/PATCH_admin_users_userId_ban.md`
- [x] **ADMIN-06** — `PATCH /api/v1/admin/users/{userId}/unban`
  - **Tên Function:** `UnbanUser` | **Role:** `RequireRole("admin")`
  - **Mô tả:** Mở khóa tài khoản đã bị đình chỉ trước đó. Hỗ trợ cả alias tương thích ngược `/api/v1/admin/user/{userId}/unban`.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminUsers/PATCH_admin_users_userId_unban.md`
- [x] **ADMIN-10** — `PUT /api/v1/admin/users/{userId}/roles`
  - **Tên Function:** `AssignUserRoles` | **Role:** `RequireRole("admin")`
  - **Mô tả:** Thay thế vai trò của tài khoản trong 4 Roles cố định (`ADMIN`, `STAFF`, `SELLER`, `BUYER`), kiểm soát Segregation of Duties (SoD - Cấm gán đồng thời `ADMIN` + `STAFF`).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminUsers/PUT_admin_users_userId_roles.md`
- [x] **ADMIN-16** — `GET /api/v1/admin/roles`
  - **Tên Function:** `ListRoles` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Xem danh sách 4 vai trò cố định của hệ thống sẵn sàng để gán.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminRoles/GET_admin_roles.md`
- [x] **ADMIN-07** — `GET /api/v1/admin/seller-registrations`
  - **Tên Function:** `ListSellerRegistrations` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Lấy danh sách hồ sơ mở Shop đang chờ thẩm định (`PENDING_APPROVAL`) có phân trang và lọc theo trạng thái.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminSellerRegistrations/GET_admin_seller_registrations.md`
- [x] **ADMIN-07B** — `GET /api/v1/admin/seller-registrations/{registrationId}`
  - **Tên Function:** `GetSellerRequestDetails` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Xem chi tiết toàn bộ hồ sơ đăng ký người bán, chứng từ và địa chỉ kho lấy hàng GHN trước khi phê duyệt.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminSellerRegistrations/GET_admin_seller_registrations_registrationId.md`
- [x] **ADMIN-08** — `PUT /api/v1/admin/seller-registrations/{id}/approve`
  - **Tên Function:** `ApproveSellerRegistration` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Phê duyệt mở Shop. Thêm role `SELLER`, khởi tạo kho GHN và thông báo kết quả.
  - **REST Nội bộ gọi đi:**
    1. `POST http://fulfillment-service:8006/api/v1/internal/warehouses`
    2. `POST http://notification-service:8007/api/v1/internal/notifications/seller-approval` (`status: "APPROVED"`)
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminSellerRegistrations/PUT_admin_seller_registrations_id_approve.md`
- [x] **ADMIN-09** — `PUT /api/v1/admin/seller-registrations/{id}/reject`
  - **Tên Function:** `RejectSellerRegistration` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Từ chối hồ sơ Shop (Bắt buộc có `rejectReason`) và gửi email thông báo cho đối tác.
  - **REST Nội bộ gọi đi:** `POST http://notification-service:8007/api/v1/internal/notifications/seller-approval` (`status: "REJECTED"`)
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminSellerRegistrations/PUT_admin_seller_registrations_id_reject.md`
- [x] **ADMIN-PENALTY-01** — `GET /api/v1/admin/penalties`
  - **Tên Function:** `GetPenalties` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Tra cứu danh sách biên bản xử phạt kỷ luật của hệ thống có phân trang và lọc theo trạng thái/loại vi phạm.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminPenalties/GET_admin_penalties.md`
- [x] **ADMIN-PENALTY-02** — `GET /api/v1/admin/penalties/{penaltyId}`
  - **Tên Function:** `GetPenaltyDetail` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Xem chi tiết toàn diện biên bản xử phạt, bằng chứng vi phạm, người tạo và lịch sử thu hồi.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminPenalties/GET_admin_penalties_penaltyId.md`
- [x] **ADMIN-PENALTY-03** — `PUT /api/v1/admin/penalties/{penaltyId}/revoke`
  - **Tên Function:** `RevokePenalty` | **Role:** `RequireRole("admin")`
  - **Mô tả:** Thu hồi án phạt kỷ luật và tự động khôi phục lại quyền hạn kinh doanh/hoạt động cho người dùng (Chỉ dành riêng cho Quản trị viên tối cao ADMIN).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminPenalties/PUT_admin_penalties_penaltyId_revoke.md`
- [x] **ADMIN-14** — `PATCH /api/v1/admin/users/{userId}/reputation`
  - **Tên Function:** `AdjustUserReputation` | **Role:** `RequireRole("admin")`
  - **Mô tả:** Can thiệp điều chỉnh điểm uy tín thủ công (Cộng/Trừ điểm kèm chỉ định nhánh vi phạm `TargetRole: Buyer/Seller`, ghi nhận lý do và cập nhật cấp bậc uy tín).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminUsers/PATCH_admin_users_userId_reputation.md`
- [x] **ADMIN-12** — `GET /api/v1/admin/configs`
  - **Tên Function:** `GetConfigs` | **Role:** `RequireRole("admin")`
  - **Mô tả:** Xem danh sách toàn bộ các tham số cấu hình động toàn sàn (Ngưỡng cọc, điểm uy tín tối thiểu, cooldown).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminConfigs/GET_admin_configs.md`
- [x] **ADMIN-13** — `PUT /api/v1/admin/configs`
  - **Tên Function:** `UpdateConfig` | **Role:** `RequireRole("admin")`
  - **Mô tả:** Cập nhật các tham số cấu hình động toàn hệ thống với kiểm tra kiểu dữ liệu và ghi vết kiểm toán.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminConfigs/PUT_admin_configs.md`
- [ ] **ADMIN-11** — `GET /api/v1/admin/audit-logs`
  - **Tên Function:** `GetAuditLogs` | **Role:** `RequireRole("admin")`
  - **Mô tả:** Tra cứu nhật ký thao tác kiểm toán bảo mật của hệ thống.
  - **Tài liệu đặc tả:** `docs/api/B_none/56_GET_ADMIN_AUDIT_LOGS.md`

---

## 🩺 NHÓM 5: GIÁM SÁT HOẠT ĐỘNG (HEALTH CHECK - 1 API)

- [x] **HEALTH-01** — `GET /health`
  - **Tên Function:** `HealthCheck` | **Role:** `AllowAnonymous`
  - **Mô tả:** Endpoint kiểm tra tình trạng sống của container phục vụ Docker Compose Orchestration và API Gateway YARP.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Health/GET_health.md`

---

## ⭐ NHÓM 6: ĐÁNH GIÁ & TỐ CÁO VI PHẠM (REVIEWS & REPORTS - 10 API)

> **Mục tiêu:** 100% RESTful API xử lý đánh giá 2 chiều sau đơn hàng (`COMPLETED`), khiếu nại tố cáo và xử lý chế tài kỷ luật vi phạm (Đã hoàn thành 8/10 APIs cốt lõi theo PR #40).

- [x] **REVIEW-01** — `POST /api/v1/reviews`
  - **Tên Function:** `CreateReview` | **Role:** `RequireRole (Authenticated - BUYER, SELLER)`
  - **Mô tả:** Đánh giá đối tác 2 chiều sau khi đơn hàng hoàn tất (`COMPLETED`). Kiểm tra phân quyền giao dịch, chống spam đánh giá lặp lại.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Reviews/POST_reviews.md`
- [x] **REVIEW-02** — `GET /api/v1/users/{userId}/reviews`
  - **Tên Function:** `GetUserReviews` | **Role:** `AllowAnonymous`
  - **Mô tả:** Xem danh sách công khai các đánh giá của cộng đồng dành cho một tài khoản có phân trang DB.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Users/GET_users_userId_reviews.md`
- [x] **REVIEW-04** — `PUT /api/v1/admin/reviews/{reviewId}/hide`
  - **Tên Function:** `HideReview` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Quản trị viên ẩn đánh giá vi phạm tiêu chuẩn cộng đồng, đồng thời kích hoạt bù trừ điểm uy tín nếu đánh giá trước đó có tác động sai.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminReviews/PUT_admin_reviews_reviewId_hide.md`
- [x] **REPORT-01** — `POST /api/v1/users/reports`
  - **Tên Function:** `CreateUserReport` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Người dùng nộp đơn khiếu nại / tố cáo tài khoản đối tác vi phạm quy chế sàn kèm mã giao dịch tham chiếu `ReferenceId`.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Users/POST_users_reports.md`
- [x] **REPORT-05** — `GET /api/v1/me/reports`
  - **Tên Function:** `GetMyReports` | **Role:** `RequireRole (Authenticated)`
  - **Mô tả:** Xem danh sách lịch sử các đơn tố cáo do chính mình đã gửi có phân trang và trạng thái xử lý.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Me/GET_me_reports.md`
- [x] **REPORT-02** — `GET /api/v1/admin/reports`
  - **Tên Function:** `GetAdminReports` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Hàng đợi đơn tố cáo vi phạm chờ Staff giải quyết có lọc theo trạng thái `PENDING`, `RESOLVED`, `DISMISSED`.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminReports/GET_admin_reports.md`
- [x] **REPORT-03** — `PUT /api/v1/admin/reports/{reportId}/resolve`
  - **Tên Function:** `ResolveReport` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Phê chuẩn đơn tố cáo, áp dụng chế tài kỷ luật tự động (Trừ điểm uy tín đúng nhánh Buyer/Seller, ghi nhận án phạt vào `UserPenalties`).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminReports/PUT_admin_reports_reportId_resolve.md`
- [x] **REPORT-04** — `PUT /api/v1/admin/reports/{reportId}/dismiss`
  - **Tên Function:** `DismissReport` | **Role:** `RequireRole("admin", "staff")`
  - **Mô tả:** Bác bỏ khiếu nại do không đủ chứng cứ hoặc sai sự thật kèm ghi chú nguyên nhân bác bỏ.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/AdminReports/PUT_admin_reports_reportId_dismiss.md`
- [x] **FILE-01** — `POST /api/v1/reports/files/upload`
  - **Tên Function:** `UploadReportFile` | **Role:** `ADMIN, STAFF, SELLER, BUYER`
  - **Mô tả:** Upload ảnh chụp màn hình / video bằng chứng tố cáo hành vi gian lận (1–5 file; ảnh <= 5MB, video MP4/WebM <= 20MB).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Users/POST_reports_files_upload.md`
- [x] **FILE-02** — `POST /api/v1/reviews/files/upload`
  - **Tên Function:** `UploadReviewFile` | **Role:** `BUYER, SELLER`
  - **Mô tả:** Upload hình ảnh sản phẩm đính kèm bài đánh giá đơn hàng / đối tác (1–5 ảnh <= 5MB).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/Reviews/POST_reviews_files_upload.md`

---

## ⚡ NHÓM 7: REST NỘI BỘ SERVICE KHÁC GỌI VÀO USER SERVICE (INBOUND REST - 8 API)

> 🚀 **THAY THẾ TOÀN BỘ gRPC VÀ RABBITMQ CONSUMER CŨ (ĐỒNG BỘ 100% VỚI V3.3):**  
> Toàn bộ các Service khác (Auction, Commerce, Fulfillment, Notification) gọi trực tiếp qua HTTP/JSON vào UserService:  
> **Base URL:** `http://user-service:8001/api/v1/internal/*`  
> **Header bảo vệ:** `X-Internal-Service-Key: <SECRET>`, `x-correlation-id: <uuid>`

- [x] **INT-GATEKEEPER** — `GET /api/v1/internal/users/{userId}/permission-check`
  - **Tên Function:** `CheckUserActionPermission` | **Service gọi:** API Gateway, Auction Service, Commerce Service
  - **Mô tả:** Cổng kiểm tra quyền hạn nội bộ thời gian thực (Gatekeeper) xem người dùng có được phép thực hiện hành động cụ thể hay không (`action: Buy, Sell, Bid, Login, Comment`) dựa trên các án phạt đang còn hiệu lực (`UserPenalties`).
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/InternalUsers/GET_internal_users_userId_permission_check.md`
- [x] **INT-01** — `POST /api/v1/internal/users/verify-bid-eligibility` & `POST /api/v1/users/verify-bid-eligibility`
  - **Tên Function:** `VerifyBidEligibility` | **Service gọi:** Auction Service (SLA < 800ms)
  - **Mô tả:** Xác thực tư cách người mua (Buyer) khi đặt giá trong phiên đấu giá. Kiểm tra tài khoản `Active`, không dính án phạt tạm thời/banned, và quỹ điểm Buyer Track `CurrentScore >= 50` (hoặc ngưỡng cấu hình). Body: `{ "userId": "uuid" }`. Trả về `{ "data": { "userId", "allowed", "failureReason" } }`.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/InternalUsers/INBOUND_INTERNAL_REST_APIS.md#1-post-apiv1internalusersverify-bid-eligibility-int-01` & `docs/api/A_Active/User/Users/POST_users_verify_bid_eligibility.md`
- [x] **INT-02** — `POST /api/v1/internal/users/batch-public-info` & `POST /api/v1/users/batch-public-info`
  - **Tên Function:** `GetBatchPublicInfo` | **Service gọi:** Auction Service, Commerce Service (Max 100 IDs)
  - **Mô tả:** Tra cứu danh sách thông tin công khai và tên che PII (`maskedName: "N***n"`, `isDeleted`, `reputationScore`, `sellerReputationScore`) phục vụ bảng lịch sử đấu giá / đơn hàng (tối đa 100 User, tự động deduplicate). Trả về `{ "data": [ ... ] }`.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/InternalUsers/INBOUND_INTERNAL_REST_APIS.md#2-post-apiv1internalusersbatch-public-info-int-02` & `docs/api/A_Active/User/Users/POST_users_batch_public_info.md`
- [x] **INT-04** — `POST /api/v1/internal/users/check-reputation` & `POST /api/v1/users/check-reputation`
  - **Tên Function:** `CheckReputation` | **Service gọi:** Auction Service
  - **Mô tả:** Kiểm tra tư cách và uy tín của Seller khi mở phiên đấu giá (Yêu cầu tài khoản `Active`, `SellerProfile` có trạng thái `Approved`, và quỹ điểm Seller Track `SellerScore >= 50`). Body: `{ "action": "SELL", "userId": "uuid" }`. Trả về `{ "data": { "userId", "allowed", "isApprovedSeller", "userStatus", "failureReason" } }`.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/InternalUsers/INBOUND_INTERNAL_REST_APIS.md#3-post-apiv1internaluserscheck-reputation-int-04` & `docs/api/A_Active/User/Users/POST_users_check_reputation.md`
- [x] **INT-06** — `POST /api/v1/internal/users/penalties`
  - **Tên Function:** `RecordPenalty` | **Service gọi:** Auction Service (bùng kèo 24h trừ 10đ), Commerce Service (Shop hủy đơn trừ 5đ)
  - **Mô tả:** Nhận yêu cầu phạt trừ điểm uy tín, lưu chính thức vào bảng `UserPenalties` và `ReputationLogs` đúng phân loại quỹ điểm `TargetRole` (`Buyer` hoặc `Seller`). Đảm bảo Idempotency tuyệt đối theo bộ khóa vi phạm.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/InternalUsers/INBOUND_INTERNAL_REST_APIS.md#4-post-apiv1internaluserspenalties-int-06`
- [x] **INT-07** — `POST /api/v1/internal/users/reputation-adjust`
  - **Tên Function:** `AdjustReputation` | **Service gọi:** Commerce/Order Service
  - **Mô tả:** Thưởng +1 điểm uy tín cho Buyer hoặc Seller khi đơn hàng hoàn tất (`OrderCompleted`), có chống cày điểm 7-Day Cooldown và bảo vệ Idempotency theo `ReferenceId`.
  - **Tài liệu đặc tả:** `docs/api/A_Active/User/InternalUsers/INBOUND_INTERNAL_REST_APIS.md#5-post-apiv1internalusersreputation-adjust-int-07`
- [ ] **INT-03** — `GET /api/v1/internal/sellers/{sellerId}/pickup-address`
  - **Service gọi:** Fulfillment Service
  - **Mô tả:** Lấy địa chỉ kho lấy hàng GHN chuẩn 4 cấp hành chính của Shop phục vụ tính phí ship và lập vận đơn (Chờ tích hợp liên Service).
  - **Tài liệu đặc tả:** `Bảo lưu lộ trình tích hợp Fulfillment Service`
- [ ] **INT-05** — `GET /api/v1/internal/users/{id}/contact`
  - **Service gọi:** Notification Service
  - **Mô tả:** Cung cấp thông tin họ tên, email, SĐT của User để gửi thông báo giao dịch (Chờ tích hợp liên Service).
  - **Tài liệu đặc tả:** `Bảo lưu lộ trình tích hợp Notification Service`

---

## 🚀 NHÓM 8: REST NỘI BỘ USER SERVICE GỌI RA SERVICE KHÁC (OUTBOUND REST - 4 API)

> 🛰️ **UserService đóng vai trò HTTP Client gọi đồng bộ sang các Microservice khác (Không dùng RabbitMQ Publisher):**

- [x] **OUT-01** — `POST http://notification-service:8007/api/v1/internal/notifications/email-otp`
  - **Mục đích:** Gửi mã xác thực OTP 6 số qua email (Dùng cho Register, Resend OTP, Forgot Password, Seller Registration).
  - **Payload:** `{ "toEmail": "string", "fullName": "string", "otpCode": "123456", "type": "REGISTER" | "FORGOT_PASSWORD" | "SELLER_REGISTRATION" }`
  - **Chịu lỗi:** Non-blocking `try-catch`, timeout 5s.
- [x] **OUT-02** — `POST http://notification-service:8007/api/v1/internal/notifications/seller-approval`
  - **Mục đích:** Báo kết quả phê duyệt hoặc từ chối hồ sơ Shop từ Staff.
  - **Payload:** `{ "toEmail": "string", "shopName": "string", "status": "APPROVED" | "REJECTED", "rejectReason": "string" }`
- [-] **OUT-03** — `POST http://wallet-service:8008/api/v1/internal/wallets`
  - **Trạng thái:** `[-] ĐÃ DECOUPLE & BÃI BỎ KHỎI USER SERVICE (PR #37, Commit 9b952e6)`
  - **Mô tả:** Đã tách rời hoàn toàn Client Wallet Service khỏi luồng Verify OTP của UserService. UserService không còn phụ thuộc hay chịu trách nhiệm mở ví trực tiếp khi người dùng kích hoạt tài khoản.
- [x] **OUT-04** — `POST http://fulfillment-service:8006/api/v1/internal/warehouses`
  - **Mục đích:** Đồng bộ thông tin kho lấy hàng GHN của Shop sang `fulfillment_db` ngay khi Staff bấm duyệt Shop.
  - **Payload:** `{ "shopId": "uuid", "shopName": "string", "pickupAddress": { ... }, "ghnShopId": 12345 }`

---

### 💡 Ghi Chú Đồng Bộ & Hướng Dẫn:
- Danh mục **63 Web & Inbound APIs** `[x]` ở trên đồng bộ chính xác 1:1 với thư mục tài liệu đặc tả `UserService/docs/api/A_Active/User/`.
- Toàn bộ các API chưa thực hiện (`[ ]`) thuộc Phase 1 mở rộng (Avatar, Đổi Email, Sessions), Upload tài liệu, và Inbound liên service được bảo lưu theo dõi tại `UserService/docs/api/B_none/`.
- Toàn bộ **402/402 bài kiểm thử xUnit tại `User.Tests` đều đã vượt qua với 0 lỗi**.
