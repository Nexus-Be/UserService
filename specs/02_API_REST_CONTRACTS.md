# 02 — HỢP ĐỒNG GIAO TIẾP RESTFUL (USER SERVICE REST CONTRACTS)

> **Trạng thái triển khai (2026-11-02):** ✅ ĐÃ CHUẨN HÓA 100% RESTFUL — Toàn bộ API của UserService là RESTful HTTP/JSON. Bãi bỏ hoàn toàn gRPC, Protobuf (`shop.proto`) và RabbitMQ / MassTransit.
> Phân quyền chặt chẽ theo Segregation of Duties (SoD):
> - Nhóm Quản trị hệ thống & tài khoản (`/api/v1/admin/users`, `/api/v1/admin/roles`): **Chỉ dành cho Admin** (`[RequireRole("admin")]`).
> - Nhóm Thẩm định & duyệt hồ sơ Seller (`/api/v1/admin/seller-registrations`): **Chỉ dành cho Support Staff** (`[RequireRole("staff")]`).
> - Nhóm Tra cứu hồ sơ người bán quản trị (`/api/v1/admin/sellers`): **Dành cho Admin & Support Staff** (`[RequireRole("admin", "staff")]`).
> **Nguồn sự thật:** code (`User.Api/Controllers/AuthController.cs`, `User.Api/Controllers/UsersController.cs`, `User.Api/Controllers/AdminUsersController.cs`, `User.Api/Controllers/AdminRolesController.cs`, `User.Api/Controllers/AdminSellerRegistrationsController.cs`, `User.Api/Controllers/AdminSellersController.cs`, `User.Api/Controllers/SellerController.cs`).
> **Cập nhật lần cuối:** 2026-11-02.
> **Tác giả:** Hiển Vinh & AI Agent

---

## 1. DANH MỤC ENDPOINTS RESTFUL CHÍNH THỨC

### 1.1. Xác thực (`AuthController` — `/api/v1/auth`)
* `POST /api/v1/auth/register`: Đăng ký tài khoản người dùng mới, sinh OTP xác thực.
* `POST /api/v1/auth/verify-otp`: Xác thực mã OTP để kích hoạt tài khoản.
* `POST /api/v1/auth/resend-otp`: Yêu cầu gửi lại mã OTP.
* `POST /api/v1/auth/login`: Đăng nhập hệ thống, cấp phát JWT access token và refresh token.
* `POST /api/v1/auth/refresh-token`: Cấp mới access token bằng refresh token.
* `POST /api/v1/auth/logout`: Đăng xuất và vô hiệu hóa token.

### 1.2. Người dùng (`UsersController` — `/api/v1/users`)
* `GET /api/v1/me`: Tra cứu thông tin hồ sơ của chính người dùng đăng nhập.
* `PUT /api/v1/me`: Cập nhật thông tin cá nhân.
* `GET /api/v1/users/{userId}`: Tra cứu thông tin công khai của một user.

### 1.3. Quản trị Người dùng & Vai trò (`AdminUsersController` & `AdminRolesController` — `/api/v1/admin/users`, `/api/v1/admin/roles`)
> **Phân quyền:** 100% Endpoints yêu cầu vai trò **Admin** (`[RequireRole("admin")]`). Role `Staff` không có quyền truy cập vào các API này.

* `POST /api/v1/admin/users`: Tạo tài khoản người dùng hoặc nhân viên mới (Admin-only).
* `GET /api/v1/admin/users`: Danh sách người dùng trong hệ thống có phân trang (Admin-only).
* `GET /api/v1/admin/users/{userId}`: Chi tiết người dùng dành cho quản trị viên (Admin-only).
* `PUT /api/v1/admin/users/{userId}`: Cập nhật thông tin người dùng bởi quản trị viên (Admin-only).
* `PUT /api/v1/admin/users/{userId}/roles`: Phân quyền vai trò người dùng (Admin-only).
* `DELETE /api/v1/admin/users/{userId}`: Xóa mềm người dùng và thu hồi tokens (Admin-only).
* `PATCH /api/v1/admin/users/{userId}/ban`: Khóa tài khoản vi phạm quy chế (Admin-only).
* `PATCH /api/v1/admin/users/{userId}/unban`: Mở khóa tài khoản đã bị đình chỉ (Admin-only).
* `GET /api/v1/admin/roles`: Danh sách toàn bộ vai trò hệ thống (`AdminRolesController` - Admin-only).

### 1.4. Quản trị Hồ sơ Đăng ký Người bán (`AdminSellerRegistrationsController` — `/api/v1/admin/seller-registrations`)
> **Phân quyền:** 100% Endpoints yêu cầu vai trò **Support Staff** (`[RequireRole("staff")]`). Role `Admin` không có quyền duyệt hồ sơ (Segregation of Duties).

* `GET /api/v1/admin/seller-registrations`: Danh sách đơn đăng ký seller chờ duyệt (Staff-only).
* `GET /api/v1/admin/seller-registrations/{registrationId}`: Chi tiết hồ sơ đơn đăng ký seller (Staff-only).
* `PUT /api/v1/admin/seller-registrations/{id}/approve`: Duyệt đơn đăng ký seller (Staff-only).
* `PUT /api/v1/admin/seller-registrations/{id}/reject`: Từ chối đơn đăng ký seller (Staff-only).

### 1.5. Quản trị Hồ sơ Người bán (`AdminSellersController` — `/api/v1/admin/sellers`)
> **Phân quyền:** Dành cho quản trị viên và nhân viên hỗ trợ (`[RequireRole("admin", "staff")]`).

* `GET /api/v1/admin/sellers/{sellerId}`: Tra cứu thông tin chi tiết hồ sơ người bán và định danh KYC dành cho quản trị viên/nhân viên (`SELLER-09`).

### 1.6. Nhà bán hàng (`SellerController` — `/api/v1/sellers`)
* `POST /api/v1/sellers/registration`: Đăng ký nâng cấp tài khoản lên Seller (Authenticated).
* `POST /api/v1/sellers/otp/verify`: Xác thực OTP email doanh nghiệp (Authenticated).
* `POST /api/v1/sellers/otp/resend`: Gửi lại mã OTP doanh nghiệp (Authenticated).
* `GET /api/v1/sellers/status`: Tra cứu tiến trình hồ sơ đăng ký seller của chính mình (Authenticated).
* `GET /api/v1/sellers/profile`: Tra cứu hồ sơ người bán (Seller hoặc Admin).
* `PATCH /api/v1/sellers/profile`: Cập nhật hồ sơ và địa chỉ kho người bán (Seller hoặc Admin).
* `GET /api/v1/sellers/{id}`: Tra cứu thông tin công khai gian hàng (Public).

---

## 2. NGUYÊN TẮC BẤT BIẾN

* **100% Plural Route Convention**: Mọi route tập hợp tài nguyên dùng danh từ số nhiều (`/api/v1/users`, `/api/v1/admin/users`, `/api/v1/sellers`, `/api/v1/admin/seller-registrations`, `/api/v1/admin/sellers`). Ngoại lệ duy nhất là contextual singleton `/api/v1/me` và nhóm xác thực phi tài nguyên `/api/v1/auth`.
* **Envelope & Errors**: Phản hồi bọc trong phong bì `ApiResponse<T>` hoặc `ApiResponse<PagedData<T>>`.
* **Giao tiếp liên service**: Gửi email OTP gọi trực tiếp sang NotificationService qua `IEmailNotificationClient` (`POST /api/v1/notifications/email/otp`).
