<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **UserService** (618 symbols, 971 relationships, 16 execution flows).

> Index stale? Run `node .gitnexus/run.cjs analyze --index-only` from the project root.

## Always Do

- **MUST run impact before editing.** Use `impact({target: "symbolName", direction: "upstream"})` or `node .gitnexus/run.cjs impact "symbolName" --direction upstream --repo .`.
- **MUST analyze graph changes before committing.** Use `detect_changes({scope: "all"})` or `node .gitnexus/run.cjs detect-changes --scope all --repo .`.
- **MUST warn on HIGH/CRITICAL `risk` pre-edit.**
- **MUST treat `risk: UNKNOWN` as unresolved, not low.**
- **NEVER** edit before impact analysis · **NEVER** rename with find-and-replace · **NEVER** commit before detect_changes.

<!-- gitnexus:end -->

---

# QUY CHUẨN VẬN HÀNH DÀNH CHO AI CODING AGENTS (USER SERVICE)

> **Trạng thái triển khai (2026-10-30):** ✅ ĐÃ CHUẨN HÓA — 100% RESTful API (`AuthController`, `UsersController`, `AdminUsersController`, `SellerController`), Feature Co-location, cấm gRPC, bãi bỏ hoàn toàn RabbitMQ, MassTransit và Outbox/Inbox tables.
> **Nguồn sự thật:** code (`User.sln`, `User.Api/Controllers/`).
> **Cập nhật lần cuối:** 2026-10-30.

---

## 🛑 NGUYÊN TẮC 1: BẮT BUỘC LẬP KẾ HOẠCH TRƯỚC KHI HÀNH ĐỘNG (PLAN-FIRST RULE)

Mọi AI Agent khi nhận nhiệm vụ kỹ thuật tại UserService **BẮT BUỘC PHẢI LẬP VÀ TRÌNH BÀY PLAN** trước khi thực hiện bất kỳ hành động sửa đổi mã nguồn, thêm/xóa file, refactor, tạo migration database hay thay đổi cấu hình nào.

Một Plan chuẩn bắt buộc phải có:
1. **Mục tiêu (Objective)**: Tóm tắt chính xác điều cần làm.
2. **Context & Files cần đọc**: Liệt kê các file nội bộ của UserService theo ma trận đọc file tại Nguyên tắc 3.
3. **Phân tích rủi ro & Phạm vi ảnh hưởng (Impact Analysis)**: Dùng GitNexus `impact`.
4. **Checklist các bước thực hiện (Step-by-Step Implementation)**.
5. **Kế hoạch kiểm chứng (Verification)**: `dotnet build User.sln -c Release` và `dotnet test User.sln -c Release`.

---

## 🧭 NGUYÊN TẮC 2: QUY TẮC PHẠM VI NỘI BỘ (INWARD-ONLY / SELF-CONTAINED)

UserService là một Git Repository độc lập:
* **TUYỆT ĐỐI CHỈ ĐƯỢC trỏ tới các tài nguyên nội bộ của UserService**.
* **TUYỆT ĐỐI KHÔNG ĐƯỢC trỏ ngược ra ngoài Root** (`../../../docs/...`) hay sang các Service khác (`../../Catalog/...`).
* Toàn bộ tài liệu, hướng dẫn và cấu hình trong repo này phải 100% tự cung tự cấp (Self-contained).

---

## 📚 NGUYÊN TẮC 3: DANH MỤC FILE NỘI BỘ BẮT BUỘC PHẢI ĐỌC TẠI USER SERVICE

Khi bắt đầu làm việc tại UserService, AI Agent **BẮT BUỘC PHẢI ĐỌC** các file nội bộ sau theo thứ tự:
1. `specs/00_AGENT_MISSION_GUARDRAILS.md`: Bounded context quản lý định danh, port `8001`, DB `usermanagement_db` (host `5433`).
2. `specs/01_CLEAN_ARCHITECTURE_LAYERS.md`: Cấu trúc 4 tầng Clean Architecture, No-Repository pattern over EF Core.
3. `specs/02_API_REST_CONTRACTS.md`: Hợp đồng RESTful API: `AuthController`, `UsersController`, `AdminUsersController`, `SellerController`.
4. `specs/03_EVENT_MESSAGING.md`: Trạng thái bãi bỏ RabbitMQ / MassTransit; gửi email qua `IEmailNotificationClient`.
5. `specs/04_DATA_PERSISTENCE.md`: Schema PostgreSQL (`Users`, `Roles`, `Permissions`, `SellerRegistrations`...).
6. `specs/05_DEV_VERIFICATION_CHECKLIST.md`: Lệnh build & test kiểm chứng service.

---

## 🔒 NGUYÊN TẮC 4: RÀO CHẮN BẤT BIẾN TẠI USER SERVICE

1. **TUYỆT ĐỐI KHÔNG DÙNG REPOSITORY PATTERN BỌC NGOÀI EF CORE**: Sử dụng trực tiếp `AppDbContext` (lớp triển khai của `IUserDbContext`) và `DbSet<T>` trong tầng Application. Cấm tạo `IUserRepository`, `GenericRepository`, `UnitOfWork`.
2. **TUYỆT ĐỐI KHÔNG LƯU MẬT KHẨU PLAIN TEXT**: Mật khẩu phải được hash bằng **BCrypt có salt** (`BCrypt.Net-Next 4.0.3`).
3. **CẤU HÌNH PORT 8001 (HTTP RESTful) VÀ DATABASE `usermanagement_db` (host port 5433) LÀ BẤT BIẾN**.
4. **CẤM SỬ DỤNG gRPC VÀ BÃI BỎ MESSAGE BROKER — 100% RESTFUL API**:
   - Toàn bộ API phục vụ giao tiếp đồng bộ qua **RESTful HTTP/JSON** (`/api/v1/auth`, `/api/v1/user`, `/api/v1/admin/user`, `/api/v1/sellers`).
   - Tuyệt đối cấm gRPC, Protobuf, RabbitMQ và MassTransit.
5. **GIAO TIẾP LIÊN SERVICE QUA TYPED REST CLIENT**:
   - Gửi OTP email gọi trực tiếp qua `IEmailNotificationClient` sang `NotificationService` (`POST /api/v1/notifications/email/otp`).
6. **FEATURE CO-LOCATION & 1-TO-1 CONTROLLER PAIRING**:
   - Tầng Application: Toàn bộ use cases đặt tại `User.Application/Services/{Feature}/` gồm `I{Feature}Service.cs`, `{Feature}Service.cs`, `{Feature}Requests.cs`, `{Feature}Responses.cs`, `{Feature}Dtos.cs`.
   - Tầng Api: Ghép cặp 1-to-1 với thin controllers tại `User.Api/Controllers/`.
7. **HẠ TẦNG REDIS DÙNG CHUNG (SHARED REDIS) — PORT 6379, 500MB**:
   - Dùng key prefix `otp:*` hoặc `user:*` để phân tách với API Gateway (`nexus:gateway:*`).
8. **QUY CHUẨN THAY ĐỔI & THAO TÁC CƠ SỞ DỮ LIỆU (DATABASE GOVERNANCE RULES)**:
   - **Enum mapping**: 100% Enum lưu dạng số nguyên `integer` trong PostgreSQL. CẤM dùng `HasConversion<string>()` hay custom enum type.
   - **Service & Domain**: 100% dùng C# Enum strongly-typed backed by `int32`. Cấm dùng số ma thuật hay chuỗi thô.
   - **REST API Outbound**: 100% tuần tự hóa thành chuỗi PascalCase qua `PascalCaseJsonStringEnumConverter`.
   - **REST API Inbound**: Nhận chuỗi qua `PascalCaseJsonStringEnumConverter` hoặc `EnumExtensions.TryToEnum<T>()`. CẤM nhận số nguyên (`allowIntegerValues: false`).
   - **Migration**: Không tự ý thêm lệnh DROP / TRUNCATE / RENAME cột khi chưa được duyệt; kiểm tra `AppDbContextModelSnapshot.cs` đồng bộ 100%.
   - **Seed Data**: Dùng GUID cố định `Guid.Parse(...)` trong `HasData()`. CẤM `Guid.NewGuid()`.

---

## 💬 NGUYÊN TẮC 5: QUY CHUẨN THÔNG BÁO LỖI TOÀN HỆ THỐNG

1. **Ngôn ngữ (100% English)**: Toàn bộ thông báo lỗi viết bằng tiếng Anh. Tuyệt đối không dùng tiếng Việt.
2. **Quy cách viết hoa (Sentence case)**: Viết hoa duy nhất chữ cái đầu câu: `"Email is already registered in the system."`
3. **Mã lỗi (SCREAMING_SNAKE_CASE)**: `EMAIL_ALREADY_EXISTS`, `OTP_EXPIRED`, `OTP_INVALID`, `OTP_ALREADY_USED`, `USER_NOT_FOUND`, `VAL_INVALID_INPUT`, `UNAUTHORIZED`, `FORBIDDEN`.

---

## 🎯 NGUYÊN TẮC 6: ĐIỀU KIỆN HOÀN THÀNH NHIỆM VỤ (DEFINITION OF DONE)

Một task tại UserService chỉ hoàn tất khi:
* Đã lập Plan chi tiết trước khi thực hiện.
* Không chứa bất kỳ liên kết trỏ ngược ra ngoài UserService.
* `dotnet build User.sln -c Release` thành công 100% (0 error).
* `dotnet test User.sln -c Release` vượt qua 100% các bài test.
* Trạng thái Git sạch, không commit file rác (`bin/`, `obj/`, `.vs/`, `.idea/`, `.env`).
