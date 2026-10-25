# USER SERVICE SPEC KIT — CHỈ DẪN KỸ THUẬT CHO AI AGENT & DEVELOPER
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — **7/7 file `.md`** trong `specs/` (README + `00`–`05`) đã được đối chiếu lại với code. Hiện trạng: **4/13 rpc** gRPC ✅, **9/13 rpc** ❌ đặc tả mục tiêu, **1/1 event publish** ✅, **0 consumer**, **không có code phát hành JWT**. Các con số trong tài liệu này lấy từ code, không lấy từ đặc tả.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
Tài liệu này là **Bộ Chỉ Dẫn & Đặc Tả Kỹ Thuật (Spec Kit)** cho repository **UserService** thuộc hệ sinh thái Microservices **Nexus**. Bộ tài liệu này giúp lập trình viên và các AI Coding Agent (Cursor, Claude, Copilot, DeepSeek...) nắm bắt bối cảnh ngay lập tức, hiểu rõ ranh giới nghiệp vụ, các quy chuẩn Clean Architecture và **loại bỏ hoàn toàn nguy cơ ảo giác (zero hallucination)** khi phát triển tính năng.
Quy ước nhãn dùng xuyên suốt bộ tài liệu:
- ✅ **CÓ** — đã tồn tại trong code và có đường dẫn `file:line` chứng minh.
- ⚠️ **MỘT PHẦN** — có khung nhưng thiếu phần lõi.
- ❌ **CHƯA CÓ** — chỉ là đặc tả mục tiêu, chưa triển khai.
---
## 1. MỤC LỤC BỘ ĐẶC TẢ USER SERVICE (SPECS/)
| File Đặc Tả | Trọng Tâm Nội Dung |
|:---|:---|
| [00_AGENT_MISSION_GUARDRAILS.md](00_AGENT_MISSION_GUARDRAILS.md) | Bounded Context, Port `8001`, DB `usermanagement_db` (`5433`), các điều cấm kỵ cốt lõi. |
| [01_CLEAN_ARCHITECTURE_LAYERS.md](01_CLEAN_ARCHITECTURE_LAYERS.md) | Cấu trúc 4 tầng Clean Architecture (`.Api`, `.Application`, `.Domain`, `.Infrastructure`), luồng phụ thuộc **thật** (kèm các vi phạm đang tồn tại). |
| [02_API_GRPC_CONTRACTS.md](02_API_GRPC_CONTRACTS.md) | Hợp đồng gRPC `UsersApi` trong `shop.proto` — **13 rpc khai báo / 4 rpc đã implement**, mã lỗi và map gRPC status thật. |
| [03_EVENT_MESSAGING.md](03_EVENT_MESSAGING.md) | Sự kiện bất đồng bộ qua RabbitMQ + MassTransit **8.3.0** (`SendOtpEmailIntegrationEvent`), Outbox/Inbox. |
| [04_DATA_PERSISTENCE.md](04_DATA_PERSISTENCE.md) | Thiết kế 14 thực thể (Entities), quan hệ bảng, DbContext của EF Core 8 (No Repository Pattern), 17 bảng trong migration. |
| [05_DEV_VERIFICATION_CHECKLIST.md](05_DEV_VERIFICATION_CHECKLIST.md) | Lệnh biên dịch, kiểm thử tự động, sanity check trước khi bàn giao. |
---
## 2. THÔNG SỐ VẬN HÀNH CHÍNH
| Thông số | Giá trị thật trong code | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|
| Ngôn ngữ & Runtime | .NET 8 (C# 12), `TargetFramework=net8.0` cho cả 5 project | ✅ | `User.Api/User.Api.csproj`, `User.Domain/User.Domain.csproj`, ... |
| Cổng gRPC Server | `8001` (h2c — HTTP/1 + HTTP/2 không TLS) | ✅ | `Dockerfile:3-4`; `User.Api/Properties/launchSettings.json`; `User.Api/Program.cs:9-16` |
| Cổng HTTP | Cùng `8001` — có `/health`, `/swagger` (chỉ Development) | ✅ | `User.Api/Program.cs:42-43,65` |
| Cơ Sở Dữ Liệu | PostgreSQL `usermanagement_db`, port `5433` | ✅ | `User.Api/appsettings.json` (`ConnectionStrings:DefaultConnection`); `docker-compose.yml` |
| Tên DB trong đặc tả cũ | `user_db` | ❌ sai | Code + Docker đều dùng `usermanagement_db` |
| Message Broker | RabbitMQ `5672`, vhost `/`, MassTransit **8.3.0** | ✅ | `User.Infrastructure/DependencyInjection.cs:37-63`; `User.Infrastructure.csproj` |
| MassTransit trong đặc tả cũ | `8.4.1` | ❌ sai | Cả 4 package MassTransit đều pin `8.3.0` |
| Thuật toán băm mật khẩu | **BCrypt** (`BCrypt.Net-Next 4.0.3`) — **không** có Argon2id | ✅ | `AuthService.cs:75,95`; không có package Argon2 nào |
| Tiêu chuẩn Token | JWT Bearer — **chưa có code phát hành** | ❌ | `IAuthService.cs:9-11`; grep `Jwt|GenerateToken|SecurityToken` trên `.cs` = 0 |
| Cấu hình JWT | Section `Jwt` có trong `appsettings.json` (`ExpirationInMinutes: 60`, Audience `shoppingcart-api`) nhưng **không code nào đọc** | ⚠️ | `User.Api/appsettings.json` |
| Kiểm thử | `dotnet test User.sln` — 9 `[Fact]`, 1 file, chỉ phủ `AuthService` | ⚠️ | `User.Tests/AuthServiceTests.cs` |
| Số file / LOC | 65 file `.cs` / 2.433 LOC (không tính `Migrations`) | ✅ | đếm trực tiếp |
| Migration | 1 file duy nhất `20261024103058_InitialUserSchema` | ✅ | `User.Infrastructure/Migrations/` |