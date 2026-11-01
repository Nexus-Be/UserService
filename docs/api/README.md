# USER SERVICE — REST API SPECIFICATION CATALOG

> **Tác giả:** Vinh & AI Agent
> **Tổng số APIs:** 35 Endpoints
> **Kiến trúc:** 100% RESTful HTTP/JSON (Cấm gRPC & Message Broker)
> **Quy chuẩn Định tuyến:** 100% Plural Route Convention (`/api/v1/users`, `/api/v1/admin/users`, `/api/v1/sellers`, `/api/v1/me`, `/api/v1/auth`)
> **Quy chuẩn Response:** Pure DTO & PagedData<T> (Gateway tự đóng gói `ApiResponse<T>`)
> **Cập nhật lần cuối:** 2026-11-01

---

## Danh mục Toàn bộ 35 REST APIs

| STT | Phương thức | Endpoint | Đường dẫn Tài liệu | Quyền hạn (RBAC) | Mô tả tóm tắt |
| :-: | :--- | :--- | :--- | :--- | :--- |
| 1 | `POST` | `/api/v1/auth/register` | [Auth/POST_register.md](Auth/POST_register.md) | `AllowAnonymous` | Registers a new user account and sends an email verification OTP. |
| 2 | `POST` | `/api/v1/users/auth/test` | [Auth/POST_users_auth_test.md](Auth/POST_users_auth_test.md) | `AllowAnonymous` | Registers a test user account with instant OTP email dispatch. |
| 3 | `POST` | `/api/v1/auth/otp/verify` | [Auth/POST_otp_verify.md](Auth/POST_otp_verify.md) | `AllowAnonymous` | Verifies the OTP sent to newly registered account to activate user. |
| 4 | `POST` | `/api/v1/auth/otp/resend` | [Auth/POST_otp_resend.md](Auth/POST_otp_resend.md) | `AllowAnonymous` | Resends the account verification OTP email with rate limiting cooldown. |
| 5 | `POST` | `/api/v1/auth/login` | [Auth/POST_login.md](Auth/POST_login.md) | `AllowAnonymous` | Authenticates a user and issues JWT access token and refresh token. |
| 6 | `POST` | `/api/v1/auth/refresh` | [Auth/POST_refresh.md](Auth/POST_refresh.md) | `AllowAnonymous` | Issues a new access token from a valid and unexpired refresh token. |
| 7 | `POST` | `/api/v1/auth/logout` | [Auth/POST_logout.md](Auth/POST_logout.md) | `AllowAnonymous` | Revokes the supplied refresh token session. |
| 8 | `POST` | `/api/v1/auth/logout-all` | [Auth/POST_logout_all.md](Auth/POST_logout_all.md) | `RequireRole (Authenticated)` | Revokes all active sessions of the authenticated user across all devices. |
| 9 | `POST` | `/api/v1/auth/password/forgot` | [Auth/POST_password_forgot.md](Auth/POST_password_forgot.md) | `AllowAnonymous` | Sends a password reset OTP code to the registered email address. |
| 10 | `PUT` | `/api/v1/auth/password/reset` | [Auth/PUT_password_reset.md](Auth/PUT_password_reset.md) | `AllowAnonymous` | Resets user account password using a verified OTP code. |
| 11 | `PATCH` | `/api/v1/auth/password` | [Auth/PATCH_password.md](Auth/PATCH_password.md) | `RequireRole (Authenticated)` | Changes the password of the currently authenticated user. |
| 12 | `GET` | `/api/v1/users/{userId}` | [Users/GET_users_userId.md](Users/GET_users_userId.md) | `AllowAnonymous` | Retrieves the safe public profile of a user by ID (USER-07). |
| 13 | `GET` | `/api/v1/me` | [Me/GET_me.md](Me/GET_me.md) | `RequireRole (Authenticated)` | Retrieves the complete profile of the currently authenticated user. |
| 14 | `PATCH` | `/api/v1/me` | [Me/PATCH_me.md](Me/PATCH_me.md) | `RequireRole (Authenticated)` | Updates editable profile fields of the authenticated user. |
| 15 | `POST` | `/api/v1/sellers/registration` | [Seller/POST_sellers_registration.md](Seller/POST_sellers_registration.md) | `RequireRole (Authenticated)` | Submits a seller onboarding registration application and triggers business email verification OTP. |
| 16 | `POST` | `/api/v1/sellers/otp/verify` | [Seller/POST_sellers_otp_verify.md](Seller/POST_sellers_otp_verify.md) | `RequireRole (Authenticated)` | Verifies seller registration OTP code and moves application into Pending review state. |
| 17 | `POST` | `/api/v1/sellers/otp/resend` | [Seller/POST_sellers_otp_resend.md](Seller/POST_sellers_otp_resend.md) | `RequireRole (Authenticated)` | Resends OTP verification code for pending seller registration draft. |
| 18 | `GET` | `/api/v1/sellers/status` | [Seller/GET_sellers_status.md](Seller/GET_sellers_status.md) | `RequireRole (Authenticated)` | Retrieves onboarding registration status of current user. |
| 19 | `GET` | `/api/v1/sellers/profile` | [Seller/GET_sellers_profile.md](Seller/GET_sellers_profile.md) | `RequireRole("seller", "admin")` | Retrieves own seller profile for the authenticated merchant. |
| 20 | `PATCH` | `/api/v1/sellers/profile` | [Seller/PATCH_sellers_profile.md](Seller/PATCH_sellers_profile.md) | `RequireRole("seller", "admin")` | Updates own seller profile and warehouse address. |
| 21 | `GET` | `/api/v1/sellers/{id}` | [Seller/GET_sellers_id.md](Seller/GET_sellers_id.md) | `AllowAnonymous` | Retrieves public shop profile for a seller by shop ID or seller user ID. |
| 22 | `POST` | `/api/v1/admin/users` | [AdminUsers/POST_admin_users.md](AdminUsers/POST_admin_users.md) | `RequireRole("admin", "staff")` | Creates a user account on behalf of an administrator. |
| 23 | `GET` | `/api/v1/admin/users` | [AdminUsers/GET_admin_users.md](AdminUsers/GET_admin_users.md) | `RequireRole("admin", "staff")` | Lists user accounts with database-level pagination, search keyword and filters. |
| 24 | `GET` | `/api/v1/admin/users/{userId}` | [AdminUsers/GET_admin_users_userId.md](AdminUsers/GET_admin_users_userId.md) | `RequireRole("admin", "staff")` | Retrieves the administrative detail of a single user account. |
| 25 | `PUT` | `/api/v1/admin/users/{userId}/roles` | [AdminUsers/PUT_admin_users_userId_roles.md](AdminUsers/PUT_admin_users_userId_roles.md) | `RequireRole("admin", "staff")` | Replaces the assigned roles of a user account with segregation of duties validation. |
| 26 | `PUT` | `/api/v1/admin/users/{userId}` | [AdminUsers/PUT_admin_users_userId.md](AdminUsers/PUT_admin_users_userId.md) | `RequireRole("admin", "staff")` | Updates an existing user account by administrator. |
| 27 | `DELETE` | `/api/v1/admin/users/{userId}` | [AdminUsers/DELETE_admin_users_userId.md](AdminUsers/DELETE_admin_users_userId.md) | `RequireRole("admin", "staff")` | Soft-deletes a user account and revokes active sessions. |
| 28 | `PATCH` | `/api/v1/admin/users/{userId}/ban` | [AdminUsers/PATCH_admin_users_userId_ban.md](AdminUsers/PATCH_admin_users_userId_ban.md) | `RequireRole("admin")` | Bans a user account for regulatory or policy violations (ADMIN-05). |
| 29 | `PATCH` | `/api/v1/admin/users/{userId}/unban` | [AdminUsers/PATCH_admin_users_userId_unban.md](AdminUsers/PATCH_admin_users_userId_unban.md) | `RequireRole("admin")` | Unbans a previously suspended user account (ADMIN-06). |
| 30 | `GET` | `/api/v1/admin/roles` | [AdminRoles/GET_admin_roles.md](AdminRoles/GET_admin_roles.md) | `RequireRole("admin", "staff")` | Lists all system user roles available for assignment. |
| 31 | `GET` | `/api/v1/admin/seller-registrations` | [AdminSellerRegistrations/GET_admin_seller_registrations.md](AdminSellerRegistrations/GET_admin_seller_registrations.md) | `RequireRole("admin", "staff")` | Lists pending seller registration applications for review with pagination. |
| 32 | `GET` | `/api/v1/admin/seller-registrations/{registrationId}` | [AdminSellerRegistrations/GET_admin_seller_registrations_registrationId.md](AdminSellerRegistrations/GET_admin_seller_registrations_registrationId.md) | `RequireRole("admin", "staff")` | Retrieves full details of a seller registration application for administrative review. |
| 33 | `PUT` | `/api/v1/admin/seller-registrations/{id}/approve` | [AdminSellerRegistrations/PUT_admin_seller_registrations_id_approve.md](AdminSellerRegistrations/PUT_admin_seller_registrations_id_approve.md) | `RequireRole("admin", "staff")` | Approves a seller registration, provisions fulfillment warehouse, and assigns Seller role. |
| 34 | `PUT` | `/api/v1/admin/seller-registrations/{id}/reject` | [AdminSellerRegistrations/PUT_admin_seller_registrations_id_reject.md](AdminSellerRegistrations/PUT_admin_seller_registrations_id_reject.md) | `RequireRole("admin", "staff")` | Rejects a seller registration application with a required reason. |
| 35 | `GET` | `/health` | [Health/GET_health.md](Health/GET_health.md) | `AllowAnonymous` | Performs service health verification for container orchestration and API gateway. |

---

## Phân bổ Theo Module Chức năng

1. **Auth (`docs/api/Auth/`):** 11 endpoints quản lý đăng ký, kích hoạt OTP, đăng nhập, JWT tokens, đổi/quên mật khẩu.
2. **Users (`docs/api/Users/`):** 1 endpoint hồ sơ an toàn công khai theo USER-07.
3. **Me (`docs/api/Me/`):** 2 endpoints quản lý hồ sơ của chính người dùng đăng nhập.
4. **Seller (`docs/api/Seller/`):** 7 endpoints quy trình onboarding người bán, xác minh OTP doanh nghiệp, và hồ sơ gian hàng.
5. **AdminUsers (`docs/api/AdminUsers/`):** 8 endpoints quản trị người dùng (danh sách phân trang, chi tiết, phân quyền vai trò, cập nhật, xóa mềm, khóa/mở khóa tài khoản).
6. **AdminRoles (`docs/api/AdminRoles/`):** 1 endpoint danh mục các vai trò hệ thống.
7. **AdminSellerRegistrations (`docs/api/AdminSellerRegistrations/`):** 4 endpoints kiểm duyệt đơn đăng ký người bán (danh sách, chi tiết, phê duyệt, từ chối).
8. **Health (`docs/api/Health/`):** 1 endpoint kiểm tra trạng thái hoạt động của microservice.
