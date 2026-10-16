# USER SERVICE SPEC KIT — CHỈ DẪN KỸ THUẬT CHO AI AGENT & DEVELOPER

Tài liệu này là **Bộ Chỉ Dẫn & Đặc Tả Kỹ Thuật (Spec Kit)** cho repository **UserService** thuộc hệ sinh thái Microservices **Nexus**. Bộ tài liệu này giúp lập trình viên và các AI Coding Agent (Cursor, Claude, Copilot, DeepSeek...) nắm bắt bối cảnh ngay lập tức, hiểu rõ ranh giới nghiệp vụ, các quy chuẩn Clean Architecture và **loại bỏ hoàn toàn nguy cơ ảo giác (zero hallucination)** khi phát triển tính năng.

---

## 1. MỤC LỤC BỘ ĐẶC TẢ USER SERVICE (SPECS/)

| File Đặc Tả | Trọng Tâm Nội Dung |
|:---|:---|
| [00_AGENT_MISSION_GUARDRAILS.md](00_AGENT_MISSION_GUARDRAILS.md) | Bounded Context, Port `8001`, DB `usermanagement_db` (`5432`), các điều cấm kỵ cốt lõi. |
| [01_CLEAN_ARCHITECTURE_LAYERS.md](01_CLEAN_ARCHITECTURE_LAYERS.md) | Cấu trúc 4 tầng Clean Architecture (`.Api`, `.Application`, `.Domain`, `.Infrastructure`), luồng phụ thuộc. |
| [02_API_GRPC_CONTRACTS.md](02_API_GRPC_CONTRACTS.md) | Hợp đồng gRPC `UsersApi` trong `shop.proto`, Request/Response messages, mã lỗi RFC 9110. |
| [03_EVENT_MESSAGING.md](03_EVENT_MESSAGING.md) | Sự kiện bất đồng bộ qua RabbitMQ + MassTransit 8.4.1 (`UserRegisteredEvent`, `SellerUpgradedEvent`), Outbox/Inbox. |
| [04_DATA_PERSISTENCE.md](04_DATA_PERSISTENCE.md) | Thiết kế thực thể (Entities), quan hệ bảng, DbContext của EF Core 8 (No Repository Pattern). |
| [05_DEV_VERIFICATION_CHECKLIST.md](05_DEV_VERIFICATION_CHECKLIST.md) | Lệnh biên dịch, kiểm thử tự động, sanity check trước khi bàn giao. |

---

## 2. THÔNG SỐ VẬN HÀNH CHÍNH

- **Ngôn ngữ & Runtime**: .NET 8 (C# 12).
- **Cổng gRPC Server**: Port `8001` (Không có cổng HTTP REST riêng, mọi REST call đều qua ApiGateway Port `8000`).
- **Cơ Sở Dữ Liệu**: PostgreSQL (Port `5432`, Database: `usermanagement_db`).
- **Message Broker**: RabbitMQ (Port `5672`, Virtual Host: `/`).
- **Thuật Toán Băm Mật Khẩu**: Argon2id hoặc BCrypt an toàn cao.
- **Tiêu Chuẩn Token**: JWT Bearer (HMAC-SHA256, Audience: `shoppingcart-api`, Expiration: 60-120 phút).
