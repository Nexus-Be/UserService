# 02 — HỢP ĐỒNG GIAO TIẾP RESTFUL (USER SERVICE REST CONTRACTS)

> **Trạng thái triển khai (2026-10-30):** ✅ ĐÃ CHUẨN HÓA 100% RESTFUL — Toàn bộ API của UserService là RESTful HTTP/JSON. Bãi bỏ hoàn toàn gRPC, Protobuf (`shop.proto`) và RabbitMQ / MassTransit.
> **Nguồn sự thật:** code (`User.Api/Controllers/AuthController.cs`, `User.Api/Controllers/UsersController.cs`, `User.Api/Controllers/AdminUsersController.cs`, `User.Api/Controllers/SellerController.cs`).
> **Cập nhật lần cuối:** 2026-10-30.

---

## 1. DANH MỤC ENDPOINTS RESTFUL CHÍNH THỨC

### 1.1. Xác thực (`AuthController` — `/api/v1/auth`)
* `POST /api/v1/auth/register`: Đăng ký tài khoản người dùng mới, sinh OTP xác thực.
* `POST /api/v1/auth/verify-otp`: Xác thực mã OTP để kích hoạt tài khoản.
* `POST /api/v1/auth/resend-otp`: Yêu cầu gửi lại mã OTP.
* `POST /api/v1/auth/login`: Đăng nhập hệ thống, cấp phát JWT access token và refresh token.
* `POST /api/v1/auth/refresh-token`: Cấp mới access token bằng refresh token.
* `POST /api/v1/auth/logout`: Đăng xuất và vô hiệu hóa token.

### 1.2. Người dùng (`UsersController` — `/api/v1/user`)
* `GET /api/v1/me`: Tra cứu thông tin hồ sơ của chính người dùng đăng nhập.
* `PUT /api/v1/me`: Cập nhật thông tin cá nhân.
* `GET /api/v1/user/{userId}`: Tra cứu thông tin công khai của một user.

### 1.3. Quản trị (`AdminUsersController` — `/api/v1/admin/user`)
* `GET /api/v1/admin/user`: Danh sách người dùng trong hệ thống có phân trang.
* `GET /api/v1/admin/user/{userId}`: Chi tiết người dùng dành cho quản trị viên.
* `PUT /api/v1/admin/user/{userId}/status`: Khóa hoặc kích hoạt lại tài khoản.

### 1.4. Nhà bán hàng (`SellerController` — `/api/v1/sellers`)
* `POST /api/v1/sellers/register`: Đăng ký nâng cấp tài khoản lên Seller.
* `GET /api/v1/sellers/profile`: Tra cứu hồ sơ người bán.

---

## 2. NGUYÊN TẮC BẤT BIẾN

* **Singular Route Convention**: Mọi route liên quan User/AdminUser dùng danh từ số ít (`/api/v1/user`, `/api/v1/admin/user`, `/api/v1/me`).
* **Envelope & Errors**: Phản hồi bọc trong phong bì `ApiResponse<T>` hoặc `ApiResponse<PagedData<T>>`.
* **Giao tiếp liên service**: Gửi email OTP gọi trực tiếp sang NotificationService qua `IEmailNotificationClient` (`POST /api/v1/notifications/email/otp`).
