# 🌐 USER SERVICE — REST API SPECIFICATION CATALOG (V3.1)

> **Tác giả:** Hiển Vinh & AI Agent  
> **Cổng Microservice:** `user-service:8001` | **Database:** `usermanagement_db:5433` | **API Gateway YARP:** `8080`  
> **Kiến trúc:** 100% RESTful HTTP/JSON (Cấm gRPC & Message Broker)  
> **Quy chuẩn Định tuyến:** 100% Plural Route Convention (`/api/v1/users`, `/api/v1/admin/users`, `/api/v1/sellers`, `/api/v1/admin/sellers`, `/api/v1/admin/penalties`, `/api/v1/me`, `/api/v1/auth`, `/api/v1/internal/users`)  
> **Phân quyền chuẩn:** 4 Fixed Roles (`ADMIN`, `STAFF`, `SELLER`, `BUYER`) — Bãi bỏ Dynamic RBAC  
> **Quy chuẩn Định danh:** 1 User - 1 Primary Role strictly enforced & Segregation of Duties (SoD)  
> **Cơ chế Quản lý 2 Nhánh:**
> - `A_Active/User/`: **42 APIs đã hoàn tất 100%** trong C# code & vượt qua 343/343 Tests.
> - `B_none/`: **32 APIs thuộc Checklist Roadmap** đang chờ triển khai / Phase 2 / Inbound REST.
> **Cập nhật lần cuối:** 2026-11-05  

---

## 📊 TIẾN ĐỘ THỰC HIỆN TỔNG QUAN

| Nhóm Nghiệp Vụ | Tổng API | Đã xong `A_Active` | Chờ làm `B_none` | Ghi chú kiến trúc & Đối soát |
| :--- | :---: | :---: | :---: | :--- |
| **1. Xác thực & Quản lý Phiên (Auth Flow)** | 13 | **11** | 2 | Hoàn thành Register, Login, Refresh, Password, Test & Logout-all. Còn Sessions |
| **2. Hồ sơ Cá nhân & Avatar (Profile Flow)** | 13 | **3** | 10 | Hoàn thành Me Profile & Public Safe Profile (`/users/{userId}`). Còn Avatar, Uy tín |
| **3. Đăng ký & Quản lý Shop (Seller Flow)** | 10 | **9** | 1 | Hoàn thành Onboarding, OTP, Status, Profile, Public Shop, Suspend & Reactivate. Còn Docs |
| **4. Quản trị User & Kỷ luật (Admin Flow)** | 20 | **17** | 3 | Hoàn thành CRUD Users, Ban/Unban, Roles 1-1, Duyệt/Từ chối Shop, Penalties. Còn Audit/Config |
| **5. Giám Sát Hoạt Động (Health Check)** | 1 | **1** | 0 | Hoàn thành Endpoint `/health` phục vụ Docker & Gateway |
| **6. Đánh giá & Tố cáo Vi phạm (Phase 2)** | 10 | **0** | 10 | 100% RESTful, giữ nguyên chờ triển khai Phase 2 |
| **7. REST Nội bộ Service Gọi Đến (Inbound REST)** | 7 | **1** | 6 | Hoàn thành `/api/v1/internal/users/{userId}/permission-check`. Còn 6 APIs |
| **TỔNG CỘNG WEB APIs** | **74 API** | **42** | **32** | **42 Web APIs trong A_Active đã Verify 100% (343 Tests Pass)** |

---

## 🟢 NHÁNH A_ACTIVE (42 ENDPOINTS ĐÃ HOÀN TẤT & KIỂM CHỨNG)

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
| 14 | `GET` | `/api/v1/users/{userId}` | [A_Active/User/Users/GET_users_userId.md](A_Active/User/Users/GET_users_userId.md) | `AllowAnonymous` | Retrieves the safe public profile of a user by ID (USER-07). |
| 15 | `POST` | `/api/v1/sellers/registration` | [A_Active/User/Seller/POST_sellers_registration.md](A_Active/User/Seller/POST_sellers_registration.md) | `RequireRole (Authenticated)` | Submits a seller onboarding registration application. |
| 16 | `POST` | `/api/v1/sellers/otp/verify` | [A_Active/User/Seller/POST_sellers_otp_verify.md](A_Active/User/Seller/POST_sellers_otp_verify.md) | `RequireRole (Authenticated)` | Verifies seller registration OTP code. |
| 17 | `POST` | `/api/v1/sellers/otp/resend` | [A_Active/User/Seller/POST_sellers_otp_resend.md](A_Active/User/Seller/POST_sellers_otp_resend.md) | `RequireRole (Authenticated)` | Resends OTP verification code for pending seller registration. |
| 18 | `GET` | `/api/v1/sellers/status` | [A_Active/User/Seller/GET_sellers_status.md](A_Active/User/Seller/GET_sellers_status.md) | `RequireRole (Authenticated)` | Retrieves onboarding registration status of current user. |
| 19 | `GET` | `/api/v1/sellers/profile` | [A_Active/User/Seller/GET_sellers_profile.md](A_Active/User/Seller/GET_sellers_profile.md) | `RequireRole("seller", "admin")` | Retrieves own seller profile for the authenticated merchant. |
| 20 | `PATCH` | `/api/v1/sellers/profile` | [A_Active/User/Seller/PATCH_sellers_profile.md](A_Active/User/Seller/PATCH_sellers_profile.md) | `RequireRole("seller", "admin")` | Updates own seller profile and warehouse address. |
| 21 | `GET` | `/api/v1/sellers/{id}` | [A_Active/User/Seller/GET_sellers_id.md](A_Active/User/Seller/GET_sellers_id.md) | `AllowAnonymous` | Retrieves public shop profile for a seller by shop ID or seller ID. |
| 22 | `POST` | `/api/v1/admin/users` | [A_Active/User/AdminUsers/POST_admin_users.md](A_Active/User/AdminUsers/POST_admin_users.md) | `RequireRole("admin", "staff")` | Creates a user account on behalf of an administrator (1 Role limit). |
| 23 | `GET` | `/api/v1/admin/users` | [A_Active/User/AdminUsers/GET_admin_users.md](A_Active/User/AdminUsers/GET_admin_users.md) | `RequireRole("admin", "staff")` | Lists user accounts with database-level pagination, search and filters. |
| 24 | `GET` | `/api/v1/admin/users/{userId}` | [A_Active/User/AdminUsers/GET_admin_users_userId.md](A_Active/User/AdminUsers/GET_admin_users_userId.md) | `RequireRole("admin", "staff")` | Retrieves administrative detail of a single user account. |
| 25 | `PUT` | `/api/v1/admin/users/{userId}` | [A_Active/User/AdminUsers/PUT_admin_users_userId.md](A_Active/User/AdminUsers/PUT_admin_users_userId.md) | `RequireRole("admin", "staff")` | Updates an existing user account by administrator. |
| 26 | `PUT` | `/api/v1/admin/users/{userId}/roles` | [A_Active/User/AdminUsers/PUT_admin_users_userId_roles.md](A_Active/User/AdminUsers/PUT_admin_users_userId_roles.md) | `RequireRole("admin")` | Replaces assigned roles of a user (Strict 1-User 1-Role & SoD validation). |
| 27 | `DELETE` | `/api/v1/admin/users/{userId}` | [A_Active/User/AdminUsers/DELETE_admin_users_userId.md](A_Active/User/AdminUsers/DELETE_admin_users_userId.md) | `RequireRole("admin", "staff")` | Soft-deletes a user account and revokes active sessions. |
| 28 | `PATCH` | `/api/v1/admin/users/{userId}/ban` | [A_Active/User/AdminUsers/PATCH_admin_users_userId_ban.md](A_Active/User/AdminUsers/PATCH_admin_users_userId_ban.md) | `RequireRole("admin")` | Bans a user account for violations, revoking all active sessions (ADMIN-05). |
| 29 | `PATCH` | `/api/v1/admin/users/{userId}/unban` | [A_Active/User/AdminUsers/PATCH_admin_users_userId_unban.md](A_Active/User/AdminUsers/PATCH_admin_users_userId_unban.md) | `RequireRole("admin")` | Unbans a previously suspended user account (ADMIN-06). |
| 30 | `GET` | `/api/v1/admin/roles` | [A_Active/User/AdminRoles/GET_admin_roles.md](A_Active/User/AdminRoles/GET_admin_roles.md) | `RequireRole("admin", "staff")` | Lists all system user roles available for assignment. |
| 31 | `GET` | `/api/v1/admin/seller-registrations` | [A_Active/User/AdminSellerRegistrations/GET_admin_seller_registrations.md](A_Active/User/AdminSellerRegistrations/GET_admin_seller_registrations.md) | `RequireRole("admin", "staff")` | Lists pending seller registration applications with pagination. |
| 32 | `GET` | `/api/v1/admin/seller-registrations/{registrationId}` | [A_Active/User/AdminSellerRegistrations/GET_admin_seller_registrations_registrationId.md](A_Active/User/AdminSellerRegistrations/GET_admin_seller_registrations_registrationId.md) | `RequireRole("admin", "staff")` | Retrieves full details of a seller registration for review. |
| 33 | `PUT` | `/api/v1/admin/seller-registrations/{id}/approve` | [A_Active/User/AdminSellerRegistrations/PUT_admin_seller_registrations_id_approve.md](A_Active/User/AdminSellerRegistrations/PUT_admin_seller_registrations_id_approve.md) | `RequireRole("admin", "staff")` | Approves seller registration, provisions warehouse, adds Seller role. |
| 34 | `PUT` | `/api/v1/admin/seller-registrations/{id}/reject` | [A_Active/User/AdminSellerRegistrations/PUT_admin_seller_registrations_id_reject.md](A_Active/User/AdminSellerRegistrations/PUT_admin_seller_registrations_id_reject.md) | `RequireRole("admin", "staff")` | Rejects a seller registration application with a required reason. |
| 35 | `GET` | `/health` | [A_Active/User/Health/GET_health.md](A_Active/User/Health/GET_health.md) | `AllowAnonymous` | Service health verification for orchestration and API gateway. |
| 36 | `GET` | `/api/v1/admin/sellers/{sellerId}` | [A_Active/User/AdminSellers/GET_admin_sellers_sellerId.md](A_Active/User/AdminSellers/GET_admin_sellers_sellerId.md) | `RequireRole("admin", "staff")` | Retrieves complete seller and KYC profile for administrators/staff (`SELLER-09`). |
| 37 | `PUT` | `/api/v1/admin/sellers/{sellerId}/suspend` | [A_Active/User/AdminSellers/PUT_admin_sellers_sellerId_suspend.md](A_Active/User/AdminSellers/PUT_admin_sellers_sellerId_suspend.md) | `RequireRole("admin", "staff")` | Suspends a merchant store (ShopStatus = Suspended). Personal buyer shopping privileges remain unaffected. |
| 38 | `PUT` | `/api/v1/admin/sellers/{sellerId}/reactivate` | [A_Active/User/AdminSellers/PUT_admin_sellers_sellerId_reactivate.md](A_Active/User/AdminSellers/PUT_admin_sellers_sellerId_reactivate.md) | `RequireRole("admin", "staff")` | Reactivates a suspended merchant store, restoring selling capabilities (ShopStatus = Active). |
| 39 | `GET` | `/api/v1/admin/penalties` | [A_Active/User/AdminPenalties/GET_admin_penalties.md](A_Active/User/AdminPenalties/GET_admin_penalties.md) | `RequireRole("admin", "staff")` | Queries paginated list of sanctions and penalty records with filters. |
| 40 | `GET` | `/api/v1/admin/penalties/{penaltyId}` | [A_Active/User/AdminPenalties/GET_admin_penalties_penaltyId.md](A_Active/User/AdminPenalties/GET_admin_penalties_penaltyId.md) | `RequireRole("admin", "staff")` | Retrieves complete details of a specific penalty record including violation evidence and revocation audit. |
| 41 | `PUT` | `/api/v1/admin/penalties/{penaltyId}/revoke` | [A_Active/User/AdminPenalties/PUT_admin_penalties_penaltyId_revoke.md](A_Active/User/AdminPenalties/PUT_admin_penalties_penaltyId_revoke.md) | `RequireRole("admin")` | Revokes an active penalty and restores associated operational privileges (Admin only). |
| 42 | `GET` | `/api/v1/internal/users/{userId}/permission-check` | [A_Active/User/InternalUsers/GET_internal_users_userId_permission_check.md](A_Active/User/InternalUsers/GET_internal_users_userId_permission_check.md) | `Internal` | Real-time permission gatekeeper for downstream microservices to verify user operational eligibility based on active sanctions. |

---

## 🟡 NHÁNH B_NONE (CHECKLIST ROADMAP CHỜ TRIỂN KHAI)

### 1. Quản lý Phiên & Hồ sơ Nâng cao (Profile & Sessions)
| Ref | Phương thức | Endpoint | Đường dẫn Tài liệu | Quyền hạn | Mô tả |
| :---: | :--- | :--- | :--- | :--- | :--- |
| `USER-03` | `GET` | `/api/v1/me/sessions` | [B_none/11_GET_ME_SESSIONS.md](B_none/11_GET_ME_SESSIONS.md) | `RequireRole` | Xem danh sách thiết bị đang đăng nhập |
| `USER-04` | `DELETE` | `/api/v1/me/sessions/{id}` | [B_none/12_DELETE_ME_SESSIONS.md](B_none/12_DELETE_ME_SESSIONS.md) | `RequireRole` | Thu hồi / Đăng xuất thiết bị từ xa |
| `USER-06B`| `GET` | `/api/v1/me/avatar` | [B_none/15_GET_ME_AVATAR.md](B_none/15_GET_ME_AVATAR.md) | `RequireRole` | Xem ảnh đại diện cá nhân |
| `USER-06` | `POST` | `/api/v1/me/avatar` | [B_none/16_POST_ME_AVATAR.md](B_none/16_POST_ME_AVATAR.md) | `RequireRole` | Tải lên ảnh đại diện cá nhân |
| `USER-08` | `GET` | `/api/v1/me/reputation` | [B_none/17_GET_ME_REPUTATION.md](B_none/17_GET_ME_REPUTATION.md) | `RequireRole` | Tra cứu điểm uy tín cá nhân |
| `USER-09` | `GET` | `/api/v1/me/penalties` | [B_none/18_GET_ME_PENALTIES.md](B_none/18_GET_ME_PENALTIES.md) | `RequireRole` | Tra cứu lịch sử biên bản xử phạt |
| `USER-07B`| `GET` | `/api/v1/users/{userId}/avatar` | [B_none/20_GET_USER_AVATAR.md](B_none/20_GET_USER_AVATAR.md) | `AllowAnonymous` | Tải nhanh avatar công khai của đối tác |
| `REVIEW-05`| `GET` | `/api/v1/users/{userId}/reputation` | [B_none/22_GET_USER_REPUTATION.md](B_none/22_GET_USER_REPUTATION.md) | `AllowAnonymous` | Xem tóm tắt điểm uy tín của đối tác |
| `REVIEW-03`| `GET` | `/api/v1/users/{userId}/reputation/history` | [B_none/23_GET_USER_REPUTATION_HISTORY.md](B_none/23_GET_USER_REPUTATION_HISTORY.md) | `RequireRole` | Xem lịch sử biến động điểm uy tín |
| `POLICY-01`| `GET` | `/api/v1/reputation/policy` | [B_none/POLICY-01_GET_REPUTATION_POLICY.md](B_none/POLICY-01_GET_REPUTATION_POLICY.md) | `AllowAnonymous` | Cung cấp thông số cấu hình ngưỡng uy tín |
| `USER-10` | `POST` | `/api/v1/me/email/change-request` | [B_none/USER-10_POST_ME_EMAIL_CHANGE_REQUEST.md](B_none/USER-10_POST_ME_EMAIL_CHANGE_REQUEST.md) | `RequireRole` | Yêu cầu đổi email đăng nhập |
| `USER-11` | `POST` | `/api/v1/me/email/change-verify` | [B_none/USER-11_POST_ME_EMAIL_CHANGE_VERIFY.md](B_none/USER-11_POST_ME_EMAIL_CHANGE_VERIFY.md) | `RequireRole` | Xác thực OTP email mới |

### 2. Hồ sơ Seller Bổ sung
| Ref | Phương thức | Endpoint | Đường dẫn Tài liệu | Quyền hạn | Mô tả |
| :---: | :--- | :--- | :--- | :--- | :--- |
| `SELLER-03`| `POST` | `/api/v1/sellers/registration/documents` | [B_none/32_POST_SELLER_REGISTRATION_DOCUMENTS.md](B_none/32_POST_SELLER_REGISTRATION_DOCUMENTS.md) | `RequireRole` | Upload ảnh CCCD và Giấy phép kinh doanh mở Shop |

### 3. Đánh giá & Tố cáo Vi phạm (Phase 2)
| Ref | Phương thức | Endpoint | Đường dẫn Tài liệu | Quyền hạn | Mô tả |
| :---: | :--- | :--- | :--- | :--- | :--- |
| `REVIEW-02`| `GET` | `/api/v1/users/{userId}/reviews` | [B_none/24_GET_USER_REVIEWS.md](B_none/24_GET_USER_REVIEWS.md) | `AllowAnonymous` | Xem danh sách đánh giá của user |
| `FILE-02`  | `POST` | `/api/v1/reviews/files/upload` | [B_none/25_POST_REVIEW_FILES_UPLOAD.md](B_none/25_POST_REVIEW_FILES_UPLOAD.md) | `RequireRole` | Upload hình ảnh đính kèm bài đánh giá |
| `REVIEW-01`| `POST` | `/api/v1/reviews` | [B_none/26_POST_REVIEWS.md](B_none/26_POST_REVIEWS.md) | `RequireRole` | Đánh giá đối tác 2 chiều sau đơn hàng |
| `FILE-01`  | `POST` | `/api/v1/reports/files/upload` | [B_none/27_POST_REPORT_FILES_UPLOAD.md](B_none/27_POST_REPORT_FILES_UPLOAD.md) | `RequireRole` | Upload bằng chứng tố cáo vi phạm |
| `REPORT-01`| `POST` | `/api/v1/users/reports` | [B_none/28_POST_USERS_REPORTS.md](B_none/28_POST_USERS_REPORTS.md) | `RequireRole` | Nộp đơn tố cáo vi phạm giao dịch |
| `REPORT-05`| `GET` | `/api/v1/me/reports` | [B_none/29_GET_ME_REPORTS.md](B_none/29_GET_ME_REPORTS.md) | `RequireRole` | Xem danh sách đơn tố cáo tôi đã gửi |
| `REPORT-02`| `GET` | `/api/v1/admin/reports` | [B_none/40_GET_ADMIN_REPORTS.md](B_none/40_GET_ADMIN_REPORTS.md) | `RequireRole("admin", "staff")` | Danh sách đơn tố cáo chờ xử lý |
| `REPORT-03`| `PUT` | `/api/v1/admin/reports/{reportId}/resolve` | [B_none/41_PUT_ADMIN_REPORTS_RESOLVE.md](B_none/41_PUT_ADMIN_REPORTS_RESOLVE.md) | `RequireRole("admin", "staff")` | Phê chuẩn đơn tố cáo vi phạm |
| `REPORT-04`| `PUT` | `/api/v1/admin/reports/{reportId}/dismiss` | [B_none/41B_PUT_ADMIN_REPORTS_DISMISS.md](B_none/41B_PUT_ADMIN_REPORTS_DISMISS.md) | `RequireRole("admin", "staff")` | Bác bỏ đơn tố cáo |
| `REVIEW-04`| `PUT` | `/api/v1/admin/reviews/{id}/hide` | [B_none/42_PUT_ADMIN_REVIEWS_HIDE.md](B_none/42_PUT_ADMIN_REVIEWS_HIDE.md) | `RequireRole("admin", "staff")` | Ẩn đánh giá vi phạm tiêu chuẩn |

### 4. Quản trị Nâng cao (Admin Flow)
| Ref | Phương thức | Endpoint | Đường dẫn Tài liệu | Quyền hạn | Mô tả |
| :---: | :--- | :--- | :--- | :--- | :--- |
| `ADMIN-14` | `PATCH` | `/api/v1/admin/users/{userId}/reputation` | [B_none/52_PATCH_ADMIN_USER_REPUTATION.md](B_none/52_PATCH_ADMIN_USER_REPUTATION.md) | `RequireRole("admin")` | Điều chỉnh điểm uy tín người dùng thủ công |
| `ADMIN-11` | `GET` | `/api/v1/admin/audit-logs` | [B_none/56_GET_ADMIN_AUDIT_LOGS.md](B_none/56_GET_ADMIN_AUDIT_LOGS.md) | `RequireRole("admin")` | Tra cứu nhật ký kiểm toán hệ thống |
| `ADMIN-12` | `GET` | `/api/v1/admin/configs` | [B_none/57_GET_ADMIN_CONFIGS.md](B_none/57_GET_ADMIN_CONFIGS.md) | `RequireRole("admin")` | Danh sách cấu hình hệ thống |
| `ADMIN-13` | `PUT` | `/api/v1/admin/configs` | [B_none/58_PUT_ADMIN_CONFIGS.md](B_none/58_PUT_ADMIN_CONFIGS.md) | `RequireRole("admin")` | Cập nhật cấu hình hệ thống |
| `ADMIN-54` | `GET` | `/api/v1/admin/permissions` | [B_none/54_GET_ADMIN_PERMISSIONS.md](B_none/54_GET_ADMIN_PERMISSIONS.md) | `RequireRole("admin")` | Danh mục permissions (Deprecated theo SoD) |
| `ADMIN-55` | `PUT` | `/api/v1/admin/roles/{roleId}/permissions` | [B_none/55_PUT_ADMIN_ROLES_PERMISSIONS.md](B_none/55_PUT_ADMIN_ROLES_PERMISSIONS.md) | `RequireRole("admin")` | Gán permissions theo vai trò (Deprecated theo SoD) |

### 5. REST Nội bộ Service Khác Gọi Vào (Inbound REST)
| Ref | Phương thức | Endpoint | Đường dẫn Tài liệu | Caller Service | Mô tả |
| :---: | :--- | :--- | :--- | :--- | :--- |
| `INT-01` | `POST` | `/api/v1/internal/users/verify-bid-eligibility` | [B_none/INT-00_INBOUND_INTERNAL_REST_APIS.md](B_none/INT-00_INBOUND_INTERNAL_REST_APIS.md) | Auction | Thẩm định quyền đặt giá đấu giá |
| `INT-02` | `POST` | `/api/v1/internal/users/batch-public-info` | [B_none/INT-00_INBOUND_INTERNAL_REST_APIS.md](B_none/INT-00_INBOUND_INTERNAL_REST_APIS.md) | Auction, Commerce | Lấy thông tin công khai hàng loạt che PII |
| `INT-03` | `GET` | `/api/v1/internal/sellers/{sellerId}/pickup-address` | [B_none/INT-00_INBOUND_INTERNAL_REST_APIS.md](B_none/INT-00_INBOUND_INTERNAL_REST_APIS.md) | Fulfillment | Lấy địa chỉ kho lấy hàng GHN của Shop |
| `INT-04` | `POST` | `/api/v1/internal/users/check-reputation` | [B_none/INT-00_INBOUND_INTERNAL_REST_APIS.md](B_none/INT-00_INBOUND_INTERNAL_REST_APIS.md) | Auction | Kiểm tra điều kiện mở đấu giá của Seller |
| `INT-05` | `GET` | `/api/v1/internal/users/{id}/contact` | [B_none/INT-00_INBOUND_INTERNAL_REST_APIS.md](B_none/INT-00_INBOUND_INTERNAL_REST_APIS.md) | Notification | Lấy họ tên, email, SĐT để gửi thông báo |
| `INT-07` | `POST` | `/api/v1/internal/users/reputation-adjust` | [B_none/INT-00_INBOUND_INTERNAL_REST_APIS.md](B_none/INT-00_INBOUND_INTERNAL_REST_APIS.md) | Commerce | Thưởng +1 uy tín khi đơn hàng COMPLETED |
