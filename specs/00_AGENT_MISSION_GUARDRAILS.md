# 00 — NHIỆM VỤ CỐT LÕI & RÀO CHẮN CHỐNG ẢO GIÁC (USER SERVICE)

> **Trạng thái triển khai (2026-11-26):** **100% RESTFUL API** — Đã hoàn thiện đầy đủ Identity & Auth (JWT/RTR, Google OAuth2), Profile & Addresses, Seller Onboarding & Lifecycle, Admin Governance (SoD), Dual-Track Reputation, Auction Sanctions, Reviews/Reports và Inbound/Outbound Internal REST.
> **Nguồn sự thật:** Codebase tại `User.Api/`, `User.Application/`, `User.Domain/`, `User.Infrastructure/`.
> **Cập nhật lần cuối:** 2026-11-26.

---

## 1. NHIỆM VỤ & RANH GIỚI MIỀN (BOUNDED CONTEXT)

**UserService** chịu trách nhiệm độc quyền về định danh, xác thực, phân quyền, hồ sơ người dùng/gian hàng, điểm uy tín và chế tài vi phạm trong hệ thống Nexus:

| # | Miền nghiệp vụ | Controller / Service chính |
|--:|:---|:---|
| 1 | Xác thực, OTP, Quên/Đổi mật khẩu, JWT + RTR, Google OAuth2 | `AuthController` / `AuthService` |
| 2 | Hồ sơ cá nhân, Avatar, Địa chỉ giao hàng GHN, Phiên đăng nhập | `MeController`, `UsersController` / `ProfileService`, `AddressService` |
| 3 | Đăng ký mở Shop, KYC, Hồ sơ công khai & Quản lý Seller | `SellerController`, `AdminSellerRegistrationsController`, `AdminSellersController` / `SellerService` |
| 4 | Quản trị User, Phân quyền 4 fixed roles & SoD, Cấu hình động | `AdminUsersController`, `AdminRolesController`, `AdminConfigsController` |
| 5 | Điểm uy tín Dual-Track (Buyer/Seller), Chế tài & Vi phạm đấu giá | `ReputationController`, `AdminPenaltiesController`, `AdminAuctionViolationsController` |
| 6 | Đánh giá 2 chiều (Reviews) & Tố cáo vi phạm / Hoàn tiền (Reports) | `ReviewsController`, `AdminReviewsController`, `AdminReportsController` |
| 7 | Giao tiếp nội bộ (Permission check, Bid/Sell eligibility, Penalties, Audit sync) | `InternalUsersController`, `InternalAuditLogsController` |

**Tên class/enum cốt lõi cần dùng:**
* DbContext triển khai `IUserDbContext` là **`AppDbContext`** (`User.Infrastructure/Persistence/AppDbContext.cs`).
* Hồ sơ gian hàng là **`SellerProfile`** (`SellerProfileStatus`: `Pending`, `Approved`, `Rejected`, `Suspended`).
* Vai trò cố định là **`RoleType`** với 4 giá trị: `Buyer`, `Seller`, `SupportStaff`, `Admin`.

---

## 2. NHỮNG ĐIỀU CẤM KỴ ĐỐI VỚI AI AGENTS (ANTI-HALLUCINATION GUARDRAILS)

1. **TUYỆT ĐỐI KHÔNG DÙNG REPOSITORY PATTERN BỌC NGOÀI EF CORE:**
   * Sử dụng trực tiếp `IUserDbContext` (`AppDbContext`) và `DbSet<T>` trong tầng Application.
   * Tuyệt đối **KHÔNG** tạo `IUserRepository`, `GenericRepository`, `UnitOfWork`.
2. **TUYỆT ĐỐI KHÔNG LƯU MẬT KHẨU PLAIN TEXT:**
   * Mật khẩu bắt buộc băm bằng **BCrypt** (`BCrypt.Net-Next 4.0.3`).
3. **CỔNG `8001` & DATABASE `usermanagement_db` (`5433`) LÀ BẤT BIẾN:**
   * Container lắng nghe `http://+:8001`; DB PostgreSQL `usermanagement_db` (host port `5433`, container port `5432`).
4. **CẤM gRPC, PROTOBUF, RABBITMQ & MASSTRANSIT:**
   * 100% API là **RESTful HTTP/JSON** theo quy chuẩn Plural Route Convention.
   * Giao tiếp liên service thực hiện đồng bộ qua Typed REST Clients (`IEmailNotificationClient`, `IFulfillmentClient`, `ICommerceClient`).
5. **TUÂN THỦ NGHIÊM NGẶT PHÂN TÁCH TRÁCH NHIỆM (SoD) & AUDIT LOGS:**
   * Không gán đồng thời `Admin` + `SupportStaff`; không tạo/gán/tước role `Seller` trực tiếp ngoài luồng Seller Onboarding/Suspension.
   * Mọi thao tác quản trị nhạy cảm bắt buộc ghi qua `_dbContext.AddAuditLog(...)`.
   * Chi tiết toàn bộ quy tắc Được làm / Không được làm xem tại [06_BUSINESS_RULES_AND_FLOWS.md](06_BUSINESS_RULES_AND_FLOWS.md).
