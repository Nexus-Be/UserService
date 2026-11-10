# 🌐 USER SERVICE — REST API SPECIFICATION CATALOG (V3.3)

> **Tác giả:** Trần Hoàng Vinh & AI Agent  
> **Cổng Microservice:** `user-service:8001` | **Database:** `usermanagement_db:5433` | **API Gateway YARP:** `8080`  
> **Kiến trúc:** 100% RESTful HTTP/JSON (Cấm gRPC & Message Broker)  
> **Quy chuẩn Định tuyến:** 100% Plural Route Convention (`/api/v1/users`, `/api/v1/admin/users`, `/api/v1/sellers`, `/api/v1/admin/sellers`, `/api/v1/admin/penalties`, `/api/v1/me`, `/api/v1/auth`, `/api/v1/internal/users`, `/api/v1/admin/configs`, `/api/v1/reviews`, `/api/v1/admin/reports`)  
> **Phân quyền chuẩn:** 4 Fixed Roles (`ADMIN`, `STAFF`, `SELLER`, `BUYER`) — Bãi bỏ Dynamic RBAC  
> **Quy chuẩn Định danh:** 1 User - 1 Primary Role strictly enforced & Segregation of Duties (SoD)  
> **Cơ chế Quản lý 2 Nhánh:**
> - `A_Active/User/`: **63 Web & Inbound APIs đã hoàn tất 100%** trong C# code & vượt qua 392/392 Tests.
> - `B_none/`: **13 APIs thuộc Roadmap** đang chờ triển khai / Phase 2 / Inbound liên Service.
> **Cập nhật lần cuối:** 2026-11-09 (PR #40 Reputation/Governance & Warehouse Identity)  

---

## 📊 TIẾN ĐỘ THỰC HIỆN TỔNG QUAN

| Nhóm Nghiệp Vụ | Tổng API | Đã xong `A_Active` | Chờ làm `B_none` | Ghi chú kiến trúc & Đối soát |
| :--- | :---: | :---: | :---: | :--- |
| **1. Xác thực & Quản lý Phiên (Auth Flow)** | 13 | **11** | 2 | Hoàn thành Register, Login, Refresh, Password, Test & Logout-all. Còn Sessions |
| **2. Hồ sơ Cá nhân & Avatar (Profile Flow)** | 13 | **8** | 5 | Hoàn thành Me Profile, Safe Profile (`/users/{userId}`), Uy tín cá nhân/công khai, Lịch sử uy tín & Policy. Còn Avatar, Đổi Email |
| **3. Đăng ký & Quản lý Shop (Seller Flow)** | 11 | **10** | 1 | Hoàn thành Onboarding, OTP, Status, Profile, Public Shop, Suspend, Reactivate & Chi tiết Admin. Còn Upload Docs CCCD |
| **4. Quản trị User & Kỷ luật (Admin Flow)** | 20 | **19** | 1 | Hoàn thành CRUD Users, Ban/Unban, Roles SoD, Duyệt/Từ chối Shop, Danh sách/Chi tiết/Thu hồi Án phạt, Chỉnh Uy tín & System Configs. Còn Audit Logs |
| **5. Giám Sát Hoạt Động (Health Check)** | 1 | **1** | 0 | Hoàn thành Endpoint `/health` phục vụ Docker & Gateway |
| **6. Đánh giá & Tố cáo Vi phạm (Reviews & Reports)** | 10 | **8** | 2 | Hoàn thành Tạo/Xem/Ẩn Review đối tác 2 chiều, Nộp/Xem/Duyệt/Bác bỏ Report tố cáo vi phạm. Còn Upload file đính kèm |
| **7. REST Nội bộ Service Gọi Đến (Inbound REST)** | 8 | **6** | 2 | Hoàn thành Gatekeeper permission-check, verify-bid-eligibility, batch-public-info, check-reputation, penalties, reputation-adjust. Còn Warehouse & Contact |
| **TỔNG CỘNG WEB APIs** | **76 API** | **63** | **13** | **Toàn bộ 63 Web & Inbound APIs trong A_Active đã Verify 100% (392/392 Tests Pass)** |

---

## 🟢 NHÁNH A_ACTIVE (63 ENDPOINTS ĐÃ HOÀN TẤT & KIỂM CHỨNG)

| STT | Phương thức | Endpoint | Đường dẫn Tài liệu | Quyền hạn (RBAC) | Mô tả tóm tắt |
| :-: | :--- | :--- | :--- | :--- | :--- |
| 1 | `POST` | `/api/v1/auth/register` | [A_Active/User/Auth/POST_register.md](A_Active/User/Auth/POST_register.md) | `AllowAnonymous` | Registers a new user account and sends an email verification OTP. |
| 2 | `POST` | `/api/v1/users/auth/test` | [A_Active/User/Auth/POST_users_auth_test.md](A_Active/User/Auth/POST_users_auth_test.md) | `AllowAnonymous` | Registers a test user account with instant OTP email dispatch. |
| 3 | `POST` | `/api/v1/auth/otp/verify` | [A_Active/User/Auth/POST_otp_verify.md](A_Active/User/Auth/POST_otp_verify.md) | `AllowAnonymous` | Verifies the OTP sent to newly registered account to activate user. |
| 4 | `POST` | `/api/v1/auth/otp/resend` | [A_Active/User/Auth/POST_otp_resend.md](A_Active/User/Auth/POST_otp_resend.md) | `AllowAnonymous` | Resends the account verification OTP email with rate limiting cooldown. |
| 5 | `POST` | `/api/v1/auth/login` | [A_Active/User/Auth/POST_login.md](A_Active/User/Auth/POST_login.md) | `AllowAnonymous` | Authenticates a user and issues JWT access token and refresh token. |
| 6 | `POST` | `/api/v1/auth/refresh` | [A_Active/User/Auth/POST_refresh.md](A_Active/User/Auth/POST_refresh.md) | `AllowAnonymous` | Issues a new access token from a valid and unexpired refresh token. |
| 7 | `POST` | `/api/v1/auth/logout` | [A_Active/User/Auth/POST_logout.md](A_Active/User/Auth/POST_logout.md) | `AllowAnonymous` | Revokes the supplied refresh token session. |
| 8 | `POST` | `/api/v1/auth/logout-all` | [A_Active/User/Auth/POST_logout_all.md](A_Active/User/Auth/POST_logout_all.md) | `RequireRole (Authenticated)` | Revokes all active sessions of the authenticated user across all devices. |
| 9 | `POST` | `/api/v1/auth/password/forgot` | [A_Active/User/Auth/POST_password_forgot.md](A_Active/User/Auth/POST_password_forgot.md) | `AllowAnonymous` | Sends a password reset OTP code to the registered email address. |
| 10 | `PUT` | `/api/v1/auth/password/reset` | [A_Active/User/Auth/PUT_password_reset.md](A_Active/User/Auth/PUT_password_reset.md) | `AllowAnonymous` | Resets user account password using a verified OTP code. |
| 11 | `PATCH` | `/api/v1/auth/password` | [A_Active/User/Auth/PATCH_password.md](A_Active/User/Auth/PATCH_password.md) | `RequireRole (Authenticated)` | Changes the password of the currently authenticated user. |
| 12 | `GET` | `/api/v1/me` | [A_Active/User/Me/GET_me.md](A_Active/User/Me/GET_me.md) | `RequireRole (Authenticated)` | Retrieves the complete profile of the currently authenticated user. |
| 13 | `PATCH` | `/api/v1/me` | [A_Active/User/Me/PATCH_me.md](A_Active/User/Me/PATCH_me.md) | `RequireRole (Authenticated)` | Updates editable profile fields of the authenticated user. |
| 14 | `GET` | `/api/v1/me/reputation` | [A_Active/User/Me/GET_me_reputation.md](A_Active/User/Me/GET_me_reputation.md) | `RequireRole (Authenticated)` | Tra cứu điểm uy tín độc lập Buyer/Seller và điều kiện tham gia đấu giá/bán hàng. |
| 15 | `GET` | `/api/v1/me/penalties` | [A_Active/User/Me/GET_me_penalties.md](A_Active/User/Me/GET_me_penalties.md) | `RequireRole (Authenticated)` | Tra cứu lịch sử các biên bản xử phạt trừ điểm uy tín của chính mình. |
| 16 | `GET` | `/api/v1/reputation/policy` | [A_Active/User/Reputation/GET_reputation_policy.md](A_Active/User/Reputation/GET_reputation_policy.md) | `AllowAnonymous` | Cung cấp thông số cấu hình ngưỡng uy tín toàn sàn cho Frontend hiển thị. |
| 17 | `GET` | `/api/v1/users/{userId}` | [A_Active/User/Users/GET_users_userId.md](A_Active/User/Users/GET_users_userId.md) | `AllowAnonymous` | Retrieves the safe public profile of a user by ID (USER-07). |
| 18 | `GET` | `/api/v1/users/{userId}/reputation` | [A_Active/User/Users/GET_users_userId_reputation.md](A_Active/User/Users/GET_users_userId_reputation.md) | `AllowAnonymous` | Xem thẻ tóm tắt điểm uy tín công khai của một đối tác. |
| 19 | `GET` | `/api/v1/users/{userId}/reputation/history` | [A_Active/User/Users/GET_users_userId_reputation_history.md](A_Active/User/Users/GET_users_userId_reputation_history.md) | `RequireRole (Authenticated)` | Xem lịch sử biến động điểm uy tín theo thời gian kèm lý do. |
| 20 | `POST` | `/api/v1/sellers/registration` | [A_Active/User/Seller/POST_sellers_registration.md](A_Active/User/Seller/POST_sellers_registration.md) | `RequireRole (Authenticated)` | Submits a seller onboarding registration application. |
| 21 | `POST` | `/api/v1/sellers/otp/verify` | [A_Active/User/Seller/POST_sellers_otp_verify.md](A_Active/User/Seller/POST_sellers_otp_verify.md) | `RequireRole (Authenticated)` | Verifies seller registration OTP code. |
| 22 | `POST` | `/api/v1/sellers/otp/resend` | [A_Active/User/Seller/POST_sellers_otp_resend.md](A_Active/User/Seller/POST_sellers_otp_resend.md) | `RequireRole (Authenticated)` | Resends OTP verification code for pending seller registration. |
| 23 | `GET` | `/api/v1/sellers/status` | [A_Active/User/Seller/GET_sellers_status.md](A_Active/User/Seller/GET_sellers_status.md) | `RequireRole (Authenticated)` | Retrieves onboarding registration status of current user. |
| 24 | `GET` | `/api/v1/sellers/profile` | [A_Active/User/Seller/GET_sellers_profile.md](A_Active/User/Seller/GET_sellers_profile.md) | `RequireRole("seller", "admin")` | Retrieves own seller profile for the authenticated merchant. |
| 25 | `PATCH` | `/api/v1/sellers/profile` | [A_Active/User/Seller/PATCH_sellers_profile.md](A_Active/User/Seller/PATCH_sellers_profile.md) | `RequireRole("seller", "admin")` | Updates own seller profile and warehouse address. |
| 26 | `GET` | `/api/v1/sellers/{id}` | [A_Active/User/Seller/GET_sellers_id.md](A_Active/User/Seller/GET_sellers_id.md) | `AllowAnonymous` | Retrieves public shop profile for a seller by shop ID or seller ID. |
| 27 | `GET` | `/api/v1/admin/sellers/{sellerId}` | [A_Active/User/AdminSellers/GET_admin_sellers_sellerId.md](A_Active/User/AdminSellers/GET_admin_sellers_sellerId.md) | `RequireRole("admin", "staff")` | Retrieves complete seller and KYC profile for administrators/staff (`SELLER-09`). |
| 28 | `PUT` | `/api/v1/admin/sellers/{sellerId}/suspend` | [A_Active/User/AdminSellers/PUT_admin_sellers_sellerId_suspend.md](A_Active/User/AdminSellers/PUT_admin_sellers_sellerId_suspend.md) | `RequireRole("admin", "staff")` | Suspends a merchant store (ShopStatus = Suspended). Buyer privileges remain intact. |
| 29 | `PUT` | `/api/v1/admin/sellers/{sellerId}/reactivate` | [A_Active/User/AdminSellers/PUT_admin_sellers_sellerId_reactivate.md](A_Active/User/AdminSellers/PUT_admin_sellers_sellerId_reactivate.md) | `RequireRole("admin", "staff")` | Reactivates a suspended merchant store, restoring selling capabilities. |
| 30 | `POST` | `/api/v1/admin/users` | [A_Active/User/AdminUsers/POST_admin_users.md](A_Active/User/AdminUsers/POST_admin_users.md) | `RequireRole("admin", "staff")` | Creates a user account on behalf of an administrator (1 Role limit). |
| 31 | `GET` | `/api/v1/admin/users` | [A_Active/User/AdminUsers/GET_admin_users.md](A_Active/User/AdminUsers/GET_admin_users.md) | `RequireRole("admin", "staff")` | Lists user accounts with database-level pagination, search and filters. |
| 32 | `GET` | `/api/v1/admin/users/{userId}` | [A_Active/User/AdminUsers/GET_admin_users_userId.md](A_Active/User/AdminUsers/GET_admin_users_userId.md) | `RequireRole("admin", "staff")` | Retrieves administrative detail of a single user account. |
| 33 | `PUT` | `/api/v1/admin/users/{userId}` | [A_Active/User/AdminUsers/PUT_admin_users_userId.md](A_Active/User/AdminUsers/PUT_admin_users_userId.md) | `RequireRole("admin", "staff")` | Updates an existing user account by administrator. |
| 34 | `PUT` | `/api/v1/admin/users/{userId}/roles` | [A_Active/User/AdminUsers/PUT_admin_users_userId_roles.md](A_Active/User/AdminUsers/PUT_admin_users_userId_roles.md) | `RequireRole("admin")` | Replaces assigned roles of a user (Strict 1-User 1-Role & SoD validation). |
| 35 | `DELETE` | `/api/v1/admin/users/{userId}` | [A_Active/User/AdminUsers/DELETE_admin_users_userId.md](A_Active/User/AdminUsers/DELETE_admin_users_userId.md) | `RequireRole("admin", "staff")` | Soft-deletes a user account and revokes active sessions. |
| 36 | `PATCH` | `/api/v1/admin/users/{userId}/ban` | [A_Active/User/AdminUsers/PATCH_admin_users_userId_ban.md](A_Active/User/AdminUsers/PATCH_admin_users_userId_ban.md) | `RequireRole("admin")` | Bans a user account for violations, revoking all active sessions (ADMIN-05). |
| 37 | `PATCH` | `/api/v1/admin/users/{userId}/unban` | [A_Active/User/AdminUsers/PATCH_admin_users_userId_unban.md](A_Active/User/AdminUsers/PATCH_admin_users_userId_unban.md) | `RequireRole("admin")` | Unbans a previously suspended user account (ADMIN-06). |
| 38 | `PATCH` | `/api/v1/admin/users/{userId}/reputation` | [A_Active/User/AdminUsers/PATCH_admin_users_userId_reputation.md](A_Active/User/AdminUsers/PATCH_admin_users_userId_reputation.md) | `RequireRole("admin")` | Điều chỉnh điểm uy tín thủ công kèm chỉ định TargetRole (Buyer/Seller). |
| 39 | `GET` | `/api/v1/admin/roles` | [A_Active/User/AdminRoles/GET_admin_roles.md](A_Active/User/AdminRoles/GET_admin_roles.md) | `RequireRole("admin", "staff")` | Lists all system user roles available for assignment. |
| 40 | `GET` | `/api/v1/admin/seller-registrations` | [A_Active/User/AdminSellerRegistrations/GET_admin_seller_registrations.md](A_Active/User/AdminSellerRegistrations/GET_admin_seller_registrations.md) | `RequireRole("admin", "staff")` | Lists pending seller registration applications with pagination. |
| 41 | `GET` | `/api/v1/admin/seller-registrations/{registrationId}` | [A_Active/User/AdminSellerRegistrations/GET_admin_seller_registrations_registrationId.md](A_Active/User/AdminSellerRegistrations/GET_admin_seller_registrations_registrationId.md) | `RequireRole("admin", "staff")` | Retrieves full details of a seller registration for review. |
| 42 | `PUT` | `/api/v1/admin/seller-registrations/{id}/approve` | [A_Active/User/AdminSellerRegistrations/PUT_admin_seller_registrations_id_approve.md](A_Active/User/AdminSellerRegistrations/PUT_admin_seller_registrations_id_approve.md) | `RequireRole("admin", "staff")` | Approves seller registration, provisions warehouse, adds Seller role. |
| 43 | `PUT` | `/api/v1/admin/seller-registrations/{id}/reject` | [A_Active/User/AdminSellerRegistrations/PUT_admin_seller_registrations_id_reject.md](A_Active/User/AdminSellerRegistrations/PUT_admin_seller_registrations_id_reject.md) | `RequireRole("admin", "staff")` | Rejects a seller registration application with a required reason. |
| 44 | `GET` | `/api/v1/admin/penalties` | [A_Active/User/AdminPenalties/GET_admin_penalties.md](A_Active/User/AdminPenalties/GET_admin_penalties.md) | `RequireRole("admin", "staff")` | Queries paginated list of sanctions and penalty records with filters. |
| 45 | `GET` | `/api/v1/admin/penalties/{penaltyId}` | [A_Active/User/AdminPenalties/GET_admin_penalties_penaltyId.md](A_Active/User/AdminPenalties/GET_admin_penalties_penaltyId.md) | `RequireRole("admin", "staff")` | Retrieves complete details of a specific penalty record including evidence and revocation audit. |
| 46 | `PUT` | `/api/v1/admin/penalties/{penaltyId}/revoke` | [A_Active/User/AdminPenalties/PUT_admin_penalties_penaltyId_revoke.md](A_Active/User/AdminPenalties/PUT_admin_penalties_penaltyId_revoke.md) | `RequireRole("admin")` | Revokes an active penalty and restores associated operational privileges (Admin only). |
| 47 | `GET` | `/api/v1/admin/configs` | [A_Active/User/AdminConfigs/GET_admin_configs.md](A_Active/User/AdminConfigs/GET_admin_configs.md) | `RequireRole("admin")` | Xem danh sách các tham số cấu hình động toàn hệ thống. |
| 48 | `PUT` | `/api/v1/admin/configs` | [A_Active/User/AdminConfigs/PUT_admin_configs.md](A_Active/User/AdminConfigs/PUT_admin_configs.md) | `RequireRole("admin")` | Cập nhật tham số cấu hình động toàn hệ thống. |
| 49 | `GET` | `/health` | [A_Active/User/Health/GET_health.md](A_Active/User/Health/GET_health.md) | `AllowAnonymous` | Service health verification for orchestration and API gateway. |
| 50 | `POST` | `/api/v1/reviews` | [A_Active/User/Reviews/POST_reviews.md](A_Active/User/Reviews/POST_reviews.md) | `RequireRole (Authenticated)` | Đánh giá đối tác 2 chiều sau đơn hàng hoàn tất. |
| 51 | `GET` | `/api/v1/users/{userId}/reviews` | [A_Active/User/Users/GET_users_userId_reviews.md](A_Active/User/Users/GET_users_userId_reviews.md) | `AllowAnonymous` | Xem danh sách công khai các đánh giá của một tài khoản. |
| 52 | `PUT` | `/api/v1/admin/reviews/{reviewId}/hide` | [A_Active/User/AdminReviews/PUT_admin_reviews_reviewId_hide.md](A_Active/User/AdminReviews/PUT_admin_reviews_reviewId_hide.md) | `RequireRole("admin", "staff")` | Quản trị viên ẩn đánh giá vi phạm và kích hoạt bù trừ điểm uy tín. |
| 53 | `POST` | `/api/v1/users/reports` | [A_Active/User/Users/POST_users_reports.md](A_Active/User/Users/POST_users_reports.md) | `RequireRole (Authenticated)` | Nộp đơn khiếu nại / tố cáo tài khoản đối tác vi phạm quy chế sàn. |
| 54 | `GET` | `/api/v1/me/reports` | [A_Active/User/Me/GET_me_reports.md](A_Active/User/Me/GET_me_reports.md) | `RequireRole (Authenticated)` | Xem danh sách lịch sử các đơn tố cáo do chính mình đã gửi. |
| 55 | `GET` | `/api/v1/admin/reports` | [A_Active/User/AdminReports/GET_admin_reports.md](A_Active/User/AdminReports/GET_admin_reports.md) | `RequireRole("admin", "staff")` | Hàng đợi danh sách đơn tố cáo vi phạm chờ Staff giải quyết. |
| 56 | `PUT` | `/api/v1/admin/reports/{reportId}/resolve` | [A_Active/User/AdminReports/PUT_admin_reports_reportId_resolve.md](A_Active/User/AdminReports/PUT_admin_reports_reportId_resolve.md) | `RequireRole("admin", "staff")` | Phê chuẩn đơn tố cáo, tự động trừ điểm uy tín và ghi nhận án phạt. |
| 57 | `PUT` | `/api/v1/admin/reports/{reportId}/dismiss` | [A_Active/User/AdminReports/PUT_admin_reports_reportId_dismiss.md](A_Active/User/AdminReports/PUT_admin_reports_reportId_dismiss.md) | `RequireRole("admin", "staff")` | Bác bỏ đơn khiếu nại tố cáo kèm lý do. |
| 58 | `GET` | `/api/v1/internal/users/{userId}/permission-check` | [A_Active/User/InternalUsers/GET_internal_users_userId_permission_check.md](A_Active/User/InternalUsers/GET_internal_users_userId_permission_check.md) | `Internal` | Real-time permission gatekeeper kiểm tra quyền hành động dựa trên án phạt hiệu lực. |
| 59 | `POST` | `/api/v1/internal/users/verify-bid-eligibility` | [A_Active/User/InternalUsers/INBOUND_INTERNAL_REST_APIS.md](A_Active/User/InternalUsers/INBOUND_INTERNAL_REST_APIS.md) | `Internal` | Thẩm định quyền đặt giá đấu giá của người mua (Buyer Track CurrentScore >= 50). |
| 60 | `POST` | `/api/v1/internal/users/batch-public-info` | [A_Active/User/InternalUsers/INBOUND_INTERNAL_REST_APIS.md](A_Active/User/InternalUsers/INBOUND_INTERNAL_REST_APIS.md) | `Internal` | Truy vấn hàng loạt thông tin công khai và tên che PII (tối đa 100 User IDs). |
| 61 | `POST` | `/api/v1/internal/users/check-reputation` | [A_Active/User/InternalUsers/INBOUND_INTERNAL_REST_APIS.md](A_Active/User/InternalUsers/INBOUND_INTERNAL_REST_APIS.md) | `Internal` | Kiểm tra điều kiện mở phiên đấu giá của Seller (Seller Track SellerScore >= 50). |
| 62 | `POST` | `/api/v1/internal/users/penalties` | [A_Active/User/InternalUsers/INBOUND_INTERNAL_REST_APIS.md](A_Active/User/InternalUsers/INBOUND_INTERNAL_REST_APIS.md) | `Internal` | Nhận lệnh phạt trừ điểm uy tín nội bộ (bùng kèo -10đ, shop hủy -5đ) có Idempotency. |
| 63 | `POST` | `/api/v1/internal/users/reputation-adjust` | [A_Active/User/InternalUsers/INBOUND_INTERNAL_REST_APIS.md](A_Active/User/InternalUsers/INBOUND_INTERNAL_REST_APIS.md) | `Internal` | Thưởng +1 uy tín khi hoàn tất đơn hàng, chống cày điểm 7-Day Cooldown. |

---

## 🟡 NHÁNH B_NONE (CHECKLIST ROADMAP CHỜ TRIỂN KHAI)

### 1. Quản lý Phiên & Hồ sơ Nâng cao (Profile & Sessions)
| Ref | Phương thức | Endpoint | Đường dẫn Tài liệu | Quyền hạn | Mô tả |
| :---: | :--- | :--- | :--- | :--- | :--- |
| `USER-03` | `GET` | `/api/v1/me/sessions` | [B_none/11_GET_ME_SESSIONS.md](B_none/11_GET_ME_SESSIONS.md) | `RequireRole` | Xem danh sách thiết bị đang đăng nhập |
| `USER-04` | `DELETE` | `/api/v1/me/sessions/{id}` | [B_none/12_DELETE_ME_SESSIONS.md](B_none/12_DELETE_ME_SESSIONS.md) | `RequireRole` | Thu hồi / Đăng xuất thiết bị từ xa |
| `USER-06B`| `GET` | `/api/v1/me/avatar` | [B_none/15_GET_ME_AVATAR.md](B_none/15_GET_ME_AVATAR.md) | `RequireRole` | Xem ảnh đại diện cá nhân |
| `USER-06` | `POST` | `/api/v1/me/avatar` | [B_none/16_POST_ME_AVATAR.md](B_none/16_POST_ME_AVATAR.md) | `RequireRole` | Tải lên ảnh đại diện cá nhân |
| `USER-07B`| `GET` | `/api/v1/users/{userId}/avatar` | [B_none/20_GET_USER_AVATAR.md](B_none/20_GET_USER_AVATAR.md) | `AllowAnonymous` | Tải nhanh avatar công khai của đối tác |
| `USER-10` | `POST` | `/api/v1/me/email/change-request` | [B_none/USER-10_POST_ME_EMAIL_CHANGE_REQUEST.md](B_none/USER-10_POST_ME_EMAIL_CHANGE_REQUEST.md) | `RequireRole` | Yêu cầu đổi email đăng nhập |
| `USER-11` | `POST` | `/api/v1/me/email/change-verify` | [B_none/USER-11_POST_ME_EMAIL_CHANGE_VERIFY.md](B_none/USER-11_POST_ME_EMAIL_CHANGE_VERIFY.md) | `RequireRole` | Xác thực OTP email mới |

### 2. Hồ sơ Seller Bổ sung
| Ref | Phương thức | Endpoint | Đường dẫn Tài liệu | Quyền hạn | Mô tả |
| :---: | :--- | :--- | :--- | :--- | :--- |
| `SELLER-03`| `POST` | `/api/v1/sellers/registration/documents` | [B_none/32_POST_SELLER_REGISTRATION_DOCUMENTS.md](B_none/32_POST_SELLER_REGISTRATION_DOCUMENTS.md) | `RequireRole` | Upload ảnh CCCD và Giấy phép kinh doanh mở Shop |

### 3. File Đính kèm Reviews & Reports (Phase 2 Upload)
| Ref | Phương thức | Endpoint | Đường dẫn Tài liệu | Quyền hạn | Mô tả |
| :---: | :--- | :--- | :--- | :--- | :--- |
| `FILE-01`  | `POST` | `/api/v1/reports/files/upload` | [B_none/27_POST_REPORT_FILES_UPLOAD.md](B_none/27_POST_REPORT_FILES_UPLOAD.md) | `RequireRole` | Upload bằng chứng tố cáo vi phạm |
| `FILE-02`  | `POST` | `/api/v1/reviews/files/upload` | [B_none/25_POST_REVIEW_FILES_UPLOAD.md](B_none/25_POST_REVIEW_FILES_UPLOAD.md) | `RequireRole` | Upload hình ảnh sản phẩm đính kèm bài đánh giá |

### 4. Quản trị Kiểm toán (Admin Flow)
| Ref | Phương thức | Endpoint | Đường dẫn Tài liệu | Quyền hạn | Mô tả |
| :---: | :--- | :--- | :--- | :--- | :--- |
| `ADMIN-11` | `GET` | `/api/v1/admin/audit-logs` | [B_none/56_GET_ADMIN_AUDIT_LOGS.md](B_none/56_GET_ADMIN_AUDIT_LOGS.md) | `RequireRole("admin")` | Tra cứu nhật ký kiểm toán hệ thống |

### 5. REST Nội bộ Service Khác Gọi Vào (Inbound Liên Service)
| Ref | Phương thức | Endpoint | Đường dẫn Tài liệu | Caller Service | Mô tả |
| :---: | :--- | :--- | :--- | :--- | :--- |
| `INT-03` | `GET` | `/api/v1/internal/sellers/{sellerId}/pickup-address` | Roadmap Fulfillment | Fulfillment | Lấy địa chỉ kho lấy hàng GHN của Shop |
| `INT-05` | `GET` | `/api/v1/internal/users/{id}/contact` | Roadmap Notification | Notification | Lấy họ tên, email, SĐT để gửi thông báo |
