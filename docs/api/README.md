# 🌐 USER SERVICE — REST API SPECIFICATION CATALOG (V3.0)

> **Tác giả:** Hiển Vinh & AI Agent  
> **Cổng Microservice:** `user-service:8001` | **Database:** `usermanagement_db:5433` | **API Gateway YARP:** `8080`  
> **Kiến trúc:** 100% RESTful HTTP/JSON (Cấm gRPC & Message Broker)  
> **Quy chuẩn Định tuyến:** 100% Plural Route Convention (`/api/v1/users`, `/api/v1/admin/users`, `/api/v1/sellers`, `/api/v1/me`, `/api/v1/auth`)  
> **Phân quyền cố định:** 4 Fixed Roles (`ADMIN`, `STAFF`, `SELLER`, `BUYER`) — Bãi bỏ Dynamic RBAC  
> **Cơ chế Quản lý 2 Nhánh:**
> - `A_Active/User/`: **35 APIs đã hoàn tất 100%** trong C# code & vượt qua 286/286 Tests.
> - `B_none/User/`: **Các APIs thuộc Checklist Roadmap** đang chờ triển khai / Phase 2 / Inbound REST.
> **Cập nhật lần cuối:** 2026-11-02  

---

## 📊 TIẾN ĐỘ THỰC HIỆN TỔNG QUAN

| Nhóm Nghiệp Vụ | Tổng API | Đã xong `A_Active` | Chờ làm `B_none` | Ghi chú kiến trúc & Đối soát |
| :--- | :---: | :---: | :---: | :--- |
| **1. Xác thực & Quản lý Phiên (Auth Flow)** | 13 | **11** | 2 | Hoàn thành Register, Login, Refresh, Password, Test & Logout-all. Còn Sessions |
| **2. Hồ sơ Cá nhân & Avatar (Profile Flow)** | 13 | **3** | 10 | Hoàn thành Me Profile & Public Safe Profile (`/users/{userId}`). Còn Avatar, Uy tín |
| **3. Đăng ký & Quản lý Shop (Seller Flow)** | 8 | **7** | 1 | Hoàn thành Onboarding, OTP verify/resend, Status, Own Profile & Public Shop. Còn Documents |
| **4. Quản trị User & Duyệt Shop (Admin Flow)** | 17 | **13** | 4 | Hoàn thành CRUD Admin Users, Ban/Unban, Roles, Duyệt/Từ chối Shop. Còn Audit & Configs |
| **5. Giám Sát Hoạt Động (Health Check)** | 1 | **1** | 0 | Hoàn thành Endpoint `/health` phục vụ Docker & Gateway |
| **6. Đánh giá & Tố cáo Vi phạm (Phase 2)** | 10 | **0** | 10 | 100% RESTful, giữ nguyên chờ triển khai Phase 2 |
| **7. REST Nội bộ Service Gọi Đến (Inbound REST)** | 7 | **0** | 7 | Thay thế gRPC & RabbitMQ, chờ tích hợp liên service |
| **TỔNG CỘNG WEB APIs** | **69 API** | **35** | **34** | **35 Web APIs trong A_Active đã Verify 100% (286 Tests Pass)** |

---

## 🟢 NHÁNH A_ACTIVE (35 ENDPOINTS ĐÃ HOÀN TẤT & KIỂM CHỨNG)

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
| 22 | `POST` | `/api/v1/admin/users` | [A_Active/User/AdminUsers/POST_admin_users.md](A_Active/User/AdminUsers/POST_admin_users.md) | `RequireRole("admin", "staff")` | Creates a user account on behalf of an administrator. |
| 23 | `GET` | `/api/v1/admin/users` | [A_Active/User/AdminUsers/GET_admin_users.md](A_Active/User/AdminUsers/GET_admin_users.md) | `RequireRole("admin", "staff")` | Lists user accounts with database-level pagination, search and filters. |
| 24 | `GET` | `/api/v1/admin/users/{userId}` | [A_Active/User/AdminUsers/GET_admin_users_userId.md](A_Active/User/AdminUsers/GET_admin_users_userId.md) | `RequireRole("admin", "staff")` | Retrieves administrative detail of a single user account. |
| 25 | `PUT` | `/api/v1/admin/users/{userId}` | [A_Active/User/AdminUsers/PUT_admin_users_userId.md](A_Active/User/AdminUsers/PUT_admin_users_userId.md) | `RequireRole("admin", "staff")` | Updates an existing user account by administrator. |
| 26 | `PUT` | `/api/v1/admin/users/{userId}/roles` | [A_Active/User/AdminUsers/PUT_admin_users_userId_roles.md](A_Active/User/AdminUsers/PUT_admin_users_userId_roles.md) | `RequireRole("admin", "staff")` | Replaces assigned roles of a user with SoD validation. |
| 27 | `DELETE` | `/api/v1/admin/users/{userId}` | [A_Active/User/AdminUsers/DELETE_admin_users_userId.md](A_Active/User/AdminUsers/DELETE_admin_users_userId.md) | `RequireRole("admin", "staff")` | Soft-deletes a user account and revokes active sessions. |
| 28 | `PATCH` | `/api/v1/admin/users/{userId}/ban` | [A_Active/User/AdminUsers/PATCH_admin_users_userId_ban.md](A_Active/User/AdminUsers/PATCH_admin_users_userId_ban.md) | `RequireRole("admin")` | Bans a user account for violations (ADMIN-05). |
| 29 | `PATCH` | `/api/v1/admin/users/{userId}/unban` | [A_Active/User/AdminUsers/PATCH_admin_users_userId_unban.md](A_Active/User/AdminUsers/PATCH_admin_users_userId_unban.md) | `RequireRole("admin")` | Unbans a previously suspended user account (ADMIN-06). |
| 30 | `GET` | `/api/v1/admin/roles` | [A_Active/User/AdminRoles/GET_admin_roles.md](A_Active/User/AdminRoles/GET_admin_roles.md) | `RequireRole("admin", "staff")` | Lists all system user roles available for assignment. |
| 31 | `GET` | `/api/v1/admin/seller-registrations` | [A_Active/User/AdminSellerRegistrations/GET_admin_seller_registrations.md](A_Active/User/AdminSellerRegistrations/GET_admin_seller_registrations.md) | `RequireRole("admin", "staff")` | Lists pending seller registration applications with pagination. |
| 32 | `GET` | `/api/v1/admin/seller-registrations/{registrationId}` | [A_Active/User/AdminSellerRegistrations/GET_admin_seller_registrations_registrationId.md](A_Active/User/AdminSellerRegistrations/GET_admin_seller_registrations_registrationId.md) | `RequireRole("admin", "staff")` | Retrieves full details of a seller registration for review. |
| 33 | `PUT` | `/api/v1/admin/seller-registrations/{id}/approve` | [A_Active/User/AdminSellerRegistrations/PUT_admin_seller_registrations_id_approve.md](A_Active/User/AdminSellerRegistrations/PUT_admin_seller_registrations_id_approve.md) | `RequireRole("admin", "staff")` | Approves seller registration, provisions warehouse, adds Seller role. |
| 34 | `PUT` | `/api/v1/admin/seller-registrations/{id}/reject` | [A_Active/User/AdminSellerRegistrations/PUT_admin_seller_registrations_id_reject.md](A_Active/User/AdminSellerRegistrations/PUT_admin_seller_registrations_id_reject.md) | `RequireRole("admin", "staff")` | Rejects a seller registration application with a required reason. |
| 35 | `GET` | `/health` | [A_Active/User/Health/GET_health.md](A_Active/User/Health/GET_health.md) | `AllowAnonymous` | Service health verification for orchestration and API gateway. |

---

## 🟡 NHÁNH B_NONE (CHECKLIST ROADMAP CHỜ TRIỂN KHAI)

### 1. Quản lý Phiên & Hồ sơ Nâng cao (Profile & Sessions)
| Ref | Phương thức | Endpoint | Đường dẫn Tài liệu | Quyền hạn | Mô tả |
| :---: | :--- | :--- | :--- | :--- | :--- |
| `USER-03` | `GET` | `/api/v1/me/sessions` | [B_none/User/11_GET_ME_SESSIONS.md](B_none/User/11_GET_ME_SESSIONS.md) | `RequireRole` | Xem danh sách thiết bị đang đăng nhập |
| `USER-04` | `DELETE` | `/api/v1/me/sessions/{id}` | [B_none/User/12_DELETE_ME_SESSIONS.md](B_none/User/12_DELETE_ME_SESSIONS.md) | `RequireRole` | Thu hồi / Đăng xuất thiết bị từ xa |
| `USER-06B`| `GET` | `/api/v1/me/avatar` | [B_none/User/15_GET_ME_AVATAR.md](B_none/User/15_GET_ME_AVATAR.md) | `RequireRole` | Xem ảnh đại diện cá nhân |
| `USER-06` | `POST` | `/api/v1/me/avatar` | [B_none/User/16_POST_ME_AVATAR.md](B_none/User/16_POST_ME_AVATAR.md) | `RequireRole` | Tải lên ảnh đại diện cá nhân |
| `USER-08` | `GET` | `/api/v1/me/reputation` | [B_none/User/17_GET_ME_REPUTATION.md](B_none/User/17_GET_ME_REPUTATION.md) | `RequireRole` | Tra cứu điểm uy tín cá nhân |
| `USER-09` | `GET` | `/api/v1/me/penalties` | [B_none/User/18_GET_ME_PENALTIES.md](B_none/User/18_GET_ME_PENALTIES.md) | `RequireRole` | Tra cứu lịch sử biên bản xử phạt |
| `USER-07B`| `GET` | `/api/v1/users/{userId}/avatar` | [B_none/User/20_GET_USER_AVATAR.md](B_none/User/20_GET_USER_AVATAR.md) | `AllowAnonymous` | Tải nhanh avatar công khai của đối tác |
| `REVIEW-05`| `GET` | `/api/v1/users/{userId}/reputation` | [B_none/User/22_GET_USER_REPUTATION.md](B_none/User/22_GET_USER_REPUTATION.md) | `AllowAnonymous` | Xem tóm tắt điểm uy tín của đối tác |
| `REVIEW-03`| `GET` | `/api/v1/users/{userId}/reputation/history` | [B_none/User/23_GET_USER_REPUTATION_HISTORY.md](B_none/User/23_GET_USER_REPUTATION_HISTORY.md) | `RequireRole` | Xem lịch sử biến động điểm uy tín |
| `POLICY-01`| `GET` | `/api/v1/reputation/policy` | [B_none/User/POLICY-01_GET_REPUTATION_POLICY.md](B_none/User/POLICY-01_GET_REPUTATION_POLICY.md) | `AllowAnonymous` | Cung cấp thông số cấu hình ngưỡng uy tín |
| `USER-10` | `POST` | `/api/v1/me/email/change-request` | [B_none/User/USER-10_POST_ME_EMAIL_CHANGE_REQUEST.md](B_none/User/USER-10_POST_ME_EMAIL_CHANGE_REQUEST.md) | `RequireRole` | Yêu cầu đổi email đăng nhập |
| `USER-11` | `POST` | `/api/v1/me/email/change-verify` | [B_none/User/USER-11_POST_ME_EMAIL_CHANGE_VERIFY.md](B_none/User/USER-11_POST_ME_EMAIL_CHANGE_VERIFY.md) | `RequireRole` | Xác thực OTP email mới |

### 2. Hồ sơ Seller Bổ sung
| Ref | Phương thức | Endpoint | Đường dẫn Tài liệu | Quyền hạn | Mô tả |
| :---: | :--- | :--- | :--- | :--- | :--- |
| `SELLER-03`| `POST` | `/api/v1/sellers/registration/documents` | [B_none/User/32_POST_SELLER_REGISTRATION_DOCUMENTS.md](B_none/User/32_POST_SELLER_REGISTRATION_DOCUMENTS.md) | `RequireRole` | Upload ảnh CCCD và Giấy phép kinh doanh mở Shop |

### 3. Đánh giá & Tố cáo Vi phạm (Phase 2)
| Ref | Phương thức | Endpoint | Đường dẫn Tài liệu | Quyền hạn | Mô tả |
| :---: | :--- | :--- | :--- | :--- | :--- |
| `REVIEW-02`| `GET` | `/api/v1/users/{userId}/reviews` | [B_none/User/24_GET_USER_REVIEWS.md](B_none/User/24_GET_USER_REVIEWS.md) | `AllowAnonymous` | Xem danh sách đánh giá của user |
| `FILE-02`  | `POST` | `/api/v1/reviews/files/upload` | [B_none/User/25_POST_REVIEW_FILES_UPLOAD.md](B_none/User/25_POST_REVIEW_FILES_UPLOAD.md) | `RequireRole` | Upload hình ảnh đính kèm bài đánh giá |
| `REVIEW-01`| `POST` | `/api/v1/reviews` | [B_none/User/26_POST_REVIEWS.md](B_none/User/26_POST_REVIEWS.md) | `RequireRole` | Đánh giá đối tác 2 chiều sau đơn hàng |
| `FILE-01`  | `POST` | `/api/v1/reports/files/upload` | [B_none/User/27_POST_REPORT_FILES_UPLOAD.md](B_none/User/27_POST_REPORT_FILES_UPLOAD.md) | `RequireRole` | Upload bằng chứng tố cáo vi phạm |
| `REPORT-01`| `POST` | `/api/v1/users/reports` | [B_none/User/28_POST_USERS_REPORTS.md](B_none/User/28_POST_USERS_REPORTS.md) | `RequireRole` | Nộp đơn tố cáo vi phạm giao dịch |
| `REPORT-05`| `GET` | `/api/v1/me/reports` | [B_none/User/29_GET_ME_REPORTS.md](B_none/User/29_GET_ME_REPORTS.md) | `RequireRole` | Xem danh sách đơn tố cáo tôi đã gửi |
| `REPORT-02`| `GET` | `/api/v1/admin/reports` | [B_none/User/40_GET_ADMIN_REPORTS.md](B_none/User/40_GET_ADMIN_REPORTS.md) | `RequireRole("admin", "staff")` | Danh sách đơn tố cáo chờ xử lý |
| `REPORT-03`| `PUT` | `/api/v1/admin/reports/{reportId}/resolve` | [B_none/User/41_PUT_ADMIN_REPORTS_RESOLVE.md](B_none/User/41_PUT_ADMIN_REPORTS_RESOLVE.md) | `RequireRole("admin", "staff")` | Phê chuẩn đơn tố cáo vi phạm |
| `REPORT-04`| `PUT` | `/api/v1/admin/reports/{reportId}/dismiss` | [B_none/User/41B_PUT_ADMIN_REPORTS_DISMISS.md](B_none/User/41B_PUT_ADMIN_REPORTS_DISMISS.md) | `RequireRole("admin", "staff")` | Bác bỏ đơn tố cáo |
| `REVIEW-04`| `PUT` | `/api/v1/admin/reviews/{id}/hide` | [B_none/User/42_PUT_ADMIN_REVIEWS_HIDE.md](B_none/User/42_PUT_ADMIN_REVIEWS_HIDE.md) | `RequireRole("admin", "staff")` | Ẩn đánh giá vi phạm tiêu chuẩn |

### 4. Quản trị Nâng cao (Admin Flow)
| Ref | Phương thức | Endpoint | Đường dẫn Tài liệu | Quyền hạn | Mô tả |
| :---: | :--- | :--- | :--- | :--- | :--- |
| `SELLER-09`| `GET` | `/api/v1/admin/sellers/{sellerId}` | [B_none/User/50_GET_ADMIN_SELLERS.md](B_none/User/50_GET_ADMIN_SELLERS.md) | `RequireRole("admin", "staff")` | Xem chi tiết hồ sơ người bán dành cho quản trị viên |
| `ADMIN-14` | `PATCH` | `/api/v1/admin/users/{userId}/reputation` | [B_none/User/52_PATCH_ADMIN_USER_REPUTATION.md](B_none/User/52_PATCH_ADMIN_USER_REPUTATION.md) | `RequireRole("admin")` | Điều chỉnh điểm uy tín người dùng thủ công |
| `ADMIN-11` | `GET` | `/api/v1/admin/audit-logs` | [B_none/User/56_GET_ADMIN_AUDIT_LOGS.md](B_none/User/56_GET_ADMIN_AUDIT_LOGS.md) | `RequireRole("admin")` | Tra cứu nhật ký kiểm toán hệ thống |
| `ADMIN-12` | `GET` | `/api/v1/admin/configs` | [B_none/User/57_GET_ADMIN_CONFIGS.md](B_none/User/57_GET_ADMIN_CONFIGS.md) | `RequireRole("admin")` | Danh sách cấu hình hệ thống |
| `ADMIN-13` | `PUT` | `/api/v1/admin/configs` | [B_none/User/58_PUT_ADMIN_CONFIGS.md](B_none/User/58_PUT_ADMIN_CONFIGS.md) | `RequireRole("admin")` | Cập nhật cấu hình hệ thống |
| `ADMIN-54` | `GET` | `/api/v1/admin/permissions` | [B_none/User/54_GET_ADMIN_PERMISSIONS.md](B_none/User/54_GET_ADMIN_PERMISSIONS.md) | `RequireRole("admin")` | Danh mục permissions (Deprecated theo SoD) |
| `ADMIN-55` | `PUT` | `/api/v1/admin/roles/{roleId}/permissions` | [B_none/User/55_PUT_ADMIN_ROLES_PERMISSIONS.md](B_none/User/55_PUT_ADMIN_ROLES_PERMISSIONS.md) | `RequireRole("admin")` | Gán permissions theo vai trò (Deprecated theo SoD) |

### 5. REST Nội bộ Service Khác Gọi Vào (Inbound REST)
| Ref | Phương thức | Endpoint | Đường dẫn Tài liệu | Caller Service | Mô tả |
| :---: | :--- | :--- | :--- | :--- | :--- |
| `INT-01` | `POST` | `/api/v1/internal/users/verify-bid-eligibility` | [B_none/User/INT-00_INBOUND_INTERNAL_REST_APIS.md](B_none/User/INT-00_INBOUND_INTERNAL_REST_APIS.md) | Auction | Thẩm định quyền đặt giá đấu giá |
| `INT-02` | `POST` | `/api/v1/internal/users/batch-public-info` | [B_none/User/INT-00_INBOUND_INTERNAL_REST_APIS.md](B_none/User/INT-00_INBOUND_INTERNAL_REST_APIS.md) | Auction, Commerce | Lấy thông tin công khai hàng loạt che PII |
| `INT-03` | `GET` | `/api/v1/internal/sellers/{sellerId}/pickup-address` | [B_none/User/INT-00_INBOUND_INTERNAL_REST_APIS.md](B_none/User/INT-00_INBOUND_INTERNAL_REST_APIS.md) | Fulfillment | Lấy địa chỉ kho lấy hàng GHN của Shop |
| `INT-04` | `POST` | `/api/v1/internal/users/check-reputation` | [B_none/User/INT-00_INBOUND_INTERNAL_REST_APIS.md](B_none/User/INT-00_INBOUND_INTERNAL_REST_APIS.md) | Auction | Kiểm tra điều kiện mở đấu giá của Seller |
| `INT-05` | `GET` | `/api/v1/internal/users/{id}/contact` | [B_none/User/INT-00_INBOUND_INTERNAL_REST_APIS.md](B_none/User/INT-00_INBOUND_INTERNAL_REST_APIS.md) | Notification | Lấy họ tên, email, SĐT để gửi thông báo |
| `INT-06` | `POST` | `/api/v1/internal/users/penalties` | [B_none/User/INT-00_INBOUND_INTERNAL_REST_APIS.md](B_none/User/INT-00_INBOUND_INTERNAL_REST_APIS.md) | Auction, Commerce | Nhận phạt trừ điểm uy tín (Idempotent) |
| `INT-07` | `POST` | `/api/v1/internal/users/reputation-adjust` | [B_none/User/INT-00_INBOUND_INTERNAL_REST_APIS.md](B_none/User/INT-00_INBOUND_INTERNAL_REST_APIS.md) | Commerce | Thưởng +1 uy tín khi đơn hàng COMPLETED |
