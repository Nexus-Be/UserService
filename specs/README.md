# USER SERVICE SPEC KIT — CHỈ DẪN KỸ THUẬT CHO AI AGENT & DEVELOPER

> **Trạng thái triển khai (2026-11-26):** **ĐÃ CHUẨN HÓA 100% RESTFUL HTTP/JSON** — Toàn bộ API của UserService giao tiếp qua RESTful trên cổng `8001`. Đã bãi bỏ hoàn toàn gRPC (`shop.proto`), RabbitMQ, MassTransit và Outbox/Inbox tables. Hỗ trợ đầy đủ JWT Bearer + Refresh Token Rotation (RTR), Google OAuth2, Seller Onboarding, Dual-Track Reputation, Auction Sanctions, Reviews/Reports và Audit Sync.
> **Nguồn sự thật:** Codebase trong repo (`User.sln`, `User.Api/Controllers/`, `User.Application/Services/`) và danh mục API tại `docs/api/API_CHECKLIST.md`.
> **Cập nhật lần cuối:** 2026-11-26.

Tài liệu này là **Bộ Chỉ Dẫn & Đặc Tả Kỹ Thuật (Spec Kit)** cho repository **UserService** thuộc hệ sinh thái Microservices **Nexus**. Bộ tài liệu giúp lập trình viên và các AI Coding Agent nắm bắt bối cảnh ngay lập tức, hiểu rõ ranh giới nghiệp vụ, các quy chuẩn Clean Architecture và **loại bỏ hoàn toàn nguy cơ ảo giác (zero hallucination)** khi phát triển tính năng.

---

## 1. MỤC LỤC BỘ ĐẶC TẢ USER SERVICE (`specs/`)

| File Đặc Tả | Trọng Tâm Nội Dung |
|:---|:---|
| [00_AGENT_MISSION_GUARDRAILS.md](00_AGENT_MISSION_GUARDRAILS.md) | Bounded Context, Port `8001`, DB `usermanagement_db` (`5433`), các điều cấm kỵ cốt lõi. |
| [01_CLEAN_ARCHITECTURE_LAYERS.md](01_CLEAN_ARCHITECTURE_LAYERS.md) | Cấu trúc 4 tầng Clean Architecture (`.Api`, `.Application`, `.Domain`, `.Infrastructure`), Feature Co-location, No-Repository over EF Core. |
| [02_API_REST_CONTRACTS.md](02_API_REST_CONTRACTS.md) | Quy chuẩn hợp đồng RESTful HTTP/JSON, Plural Route Convention, phân quyền SoD (chi tiết 100% endpoints xem tại `docs/api/API_CHECKLIST.md`). |
| [03_EVENT_MESSAGING.md](03_EVENT_MESSAGING.md) | Bãi bỏ hoàn toàn RabbitMQ / MassTransit; giao tiếp đồng bộ qua Typed REST Clients (`IEmailNotificationClient`, `IFulfillmentClient`, `ICommerceClient`). |
| [04_DATA_PERSISTENCE.md](04_DATA_PERSISTENCE.md) | Thiết kế thực thể (Entities), quan hệ bảng, `AppDbContext` (`IUserDbContext`), integer enums & deterministic seed GUIDs. |
| [05_DEV_VERIFICATION_CHECKLIST.md](05_DEV_VERIFICATION_CHECKLIST.md) | Lệnh biên dịch, kiểm thử tự động (`dotnet build` & `dotnet test User.sln -c Release`) trước khi bàn giao. |
| [06_BUSINESS_RULES_AND_FLOWS.md](06_BUSINESS_RULES_AND_FLOWS.md) | **Nghiệp vụ, luồng xử lý, máy trạng thái & quy tắc Được làm / Không được làm** (RBAC SoD, Auth/RTR, Seller onboarding, Dual-track Reputation, Auction streak, Reviews/Reports). |

---

## 2. THÔNG SỐ VẬN HÀNH CHÍNH

| Thông số | Giá trị thật trong code | Bằng chứng |
|:---|:---|:---|
| Ngôn ngữ & Runtime | .NET 8 (C# 12), `TargetFramework=net8.0` | `User.Api/User.Api.csproj`, `User.Domain/User.Domain.csproj` |
| Giao thức & Cổng | **100% RESTful HTTP/JSON**, cổng `8001` (có `/health`, `/swagger` dev) | `User.Api/Program.cs`, `Dockerfile`, `Properties/launchSettings.json` |
| Cơ Sở Dữ Liệu | PostgreSQL `usermanagement_db` (container `5432`, host publish `5433`) | `User.Api/appsettings.json`, `docker-compose.yml` |
| Redis Cache | Shared Redis port `6379` (key prefix `otp:*`, `user:*`) | `User.Infrastructure/` |
| Liên Service | Typed REST Clients (`HttpClientFactory`, không gRPC, không RabbitMQ) | `User.Infrastructure/Services/Notification/EmailNotificationClient.cs` |
| Thuật toán băm mật khẩu | **BCrypt** (`BCrypt.Net-Next 4.0.3`) | `AuthService.cs` |
| Tiêu chuẩn Token | JWT Bearer (HS256) + Refresh Token Rotation (RTR) | `JwtTokenGenerator.cs`, `AuthService.cs` |
| Kiểm thử | `dotnet test User.sln -c Release` | `User.Tests/` |
