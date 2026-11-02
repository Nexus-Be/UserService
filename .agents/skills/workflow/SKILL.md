---
name: workflow
description: >-
  Standardized mentor-guided development workflow: defines pre-task clarification (user stories, API contract alignment between ApiGateway & UserService BE, mandatory AGENTS.md check, strict migration & secret safety guardrails), focused code change documentation, repetitive error/bug registry tracking (Troubleshooting), Git commit/push with commit hash tracking, and automated separation of task logs from core theory/concept notes with timestamps into Obsidian vault.
---

# Dev Workflow & Logging Skill (Obsidian Vault Edition)

Kỹ năng chuẩn hóa phương pháp học tập và phát triển mã nguồn theo phong cách Mentor hướng dẫn học viên, kết nối trực tiếp với Obsidian Vault:
1. **Trước khi làm (Pha 1):** Bắt buộc đọc `AGENTS.md`, xác định User Story, đối chiếu hợp đồng RESTful, lập Plan và tuân thủ rào chắn bất biến (không sửa migration cũ, không đụng secret, 100% RESTful).
2. **Trong khi làm (Pha 2):** Kiểm soát lỗi nghiêm ngặt — Nếu code/test bị lỗi $\ge 2$ lần hoặc gặp bẫy hồi quy (regression), **bắt buộc ghi nhận ngay vào Sổ tay sự cố (`Troubleshooting/`)** để phòng ngừa lặp lại.
3. **Sau khi làm (Pha 3):** Tóm tắt thay đổi code cốt lõi, trích xuất mã **Commit Hash** (`git log -1 --oneline`), và tự động chiết xuất lý thuyết mới vào `Concepts/`.
4. **Lưu trữ chuẩn trong Obsidian Vault:**
   - `Tasks/`: Thực chiến công việc theo ngày giờ (`YYYY-MM-DD_HHmm_[task].md`).
   - `Concepts/`: Sổ tay lý thuyết & nguyên lý (`YYYY-MM-DD_[concept].md`).
   - `Troubleshooting/`: Sổ tay sự cố & bẫy lỗi thường gặp (`YYYY-MM-DD_[error-name].md` và Master Index).
   - `Flows/`: Bản đồ truy vết luồng hệ thống End-to-End (`YYYY-MM-DD_flow_[ten-luong].md`).

---

## 1. Cấu trúc thư mục lưu trữ Obsidian Vault

Toàn bộ tài liệu ghi chép được lưu tại Obsidian Vault: `D:/obsidian/vinhth/UserService - học/`:

```
D:/obsidian/vinhth/UserService - học/
├── Tasks/                      # Nhật ký thực chiến từng task (kèm commit hash)
│   └── YYYY-MM-DD_HHmm_[ten-task].md
├── Concepts/                   # Sổ tay lý thuyết, kiến trúc, mẫu thiết kế (chỉ dùng ngày)
│   ├── 00_LO_TRINH_DOC_HOC_CONCEPTS_PROJECT_NEXUS.md
│   └── YYYY-MM-DD_[ten-khai-niem].md
├── Troubleshooting/            # Sổ tay sự cố, cạm bẫy lặp lại & cách giải quyết
│   ├── 00_SO_TAY_TRA_CUU_LOI_THUONG_GAP.md
│   └── YYYY-MM-DD_[ten-loi-va-cach-fix].md
└── Flows/                      # Bản đồ truy vết luồng hệ thống End-to-End
    └── YYYY-MM-DD_flow_[ten-luong].md
```

---

## 2. Rào chắn Bất biến Bắt buộc Tuân thủ (Guardrail Reference)

Để tránh phân mảnh và đảm bảo nhất quán tuyệt đối, AI Agent bắt buộc tham chiếu trực tiếp các rào chắn từ nguồn chuyên biệt thay vì chép lại:
1. **Rào chắn Codebase & Nguồn sự thật:** Tuân thủ 100% [01_codebase_guardrails.md](../rules/01_codebase_guardrails.md):
   - Thứ bậc ưu tiên: `specs/` (trong repo) là tối cao (`specs/` thắng), mâu thuẫn thì báo cáo.
   - Bắt buộc 100% RESTful HTTP/JSON (Cấm gRPC).
   - Tuyệt đối cấm tự ý sửa `.env`, connection string, secret keys.
   - Cấm các lệnh phá hủy (`git push --force`, `git reset --hard`, `dotnet ef database drop`, xóa file hàng loạt).
   - Cấm `git add .` mù quáng. Bắt buộc `git status` trước rồi add đúng file.
2. **Rào chắn CSDL & Migration An toàn:** Tuân thủ quy trình tại Mục 7 (Tuyệt đối cấm sửa file migration cũ).
3. **Rào chắn Giao tiếp Liên-Service & Kiến trúc Đồng bộ (100% RESTful):** Toàn bộ sàn Nexus đã **bỏ hoàn toàn RabbitMQ / MassTransit Outbox**. Mọi tương tác liên-service phải đi qua RESTful HTTP Client (`IHttpClientFactory` với Timeout / Retry bằng Polly) hoặc xử lý tác vụ ngầm nội bộ (`System.Threading.Channels` + `BackgroundService`). Cấm thêm mới bất kỳ cấu hình Message Broker nào.
4. **Kiểm tra Banner Trạng thái trong `specs/`:** Đọc các banner `[✅ CÓ TRONG CODE]` và `[❌ CHƯA CÓ]` trong file specs liên quan trực tiếp tới task để nắm chắc hiện trạng thật, không ảo giác.
5. **Rào chắn Phạm vi Kỹ năng Local (Local Skill Isolation - BẤT BIẾN):** Các kỹ năng và quy tắc trong thư mục `.agents/` (được bảo vệ bởi `.gitignore`) là cấu hình cục bộ của máy phát triển cá nhân dùng để hướng dẫn AI Agent. **TUYỆT ĐỐI CẤM SỬA ĐỔI FILE `AGENTS.md` HOẶC CÁC FILE MÃ NGUỒN CỦA BASE REPOSITORY ĐỂ ĐƯA QUY TẮC SKILL VÀO**. Mọi cấu hình workflow cá nhân chỉ được lưu trữ nội bộ trong `.agents/skills/` hoặc Obsidian Vault.

---

## 3. Ma trận Tích Hợp Đồng Bộ Chuẩn (100% Synchronous Architecture)

| Kênh tích hợp | Đối tượng & Mục đích | Giao thức / Công nghệ | Vị trí Code trong UserService |
| :--- | :--- | :--- | :--- |
| **🌐 1. Ingress REST API (Sync)** | • **Client / Gateway:** Người dùng gọi qua Gateway vào `/api/v1/...`<br>• **Service Nội bộ (Inter-Service Ingress):** Auction/Order Service gọi lấy dữ liệu tức thì qua `/api/v1/internal/...` | **HTTP/1.1 hoặc HTTP/2 + JSON** | `User.Api/Controllers/`<br>(vd: `AuthController.cs`, `UsersController.cs`) |
| **🔗 2. Egress REST API (Liên-Service Sync)** | UserService gọi sang NotificationService (gửi mail OTP) hoặc InventoryService (tạo kho khi duyệt Seller). | **HttpClientFactory + Polly (Timeout/Retry)** | `User.Application/Common/Interfaces/` & `User.Infrastructure/Services/External/` |
| **⚡ 3. In-Memory Background Tasks** | Xử lý tác vụ ngầm trong chính tiến trình UserService (nếu tự gửi SMTP hoặc dọn dẹp cache không qua broker). | **System.Threading.Channels + BackgroundService** | `User.Infrastructure/BackgroundJobs/` |
| **⚠️ 4. RabbitMQ (Legacy - Bị Hủy)** | Đã bỏ hoàn toàn trên toàn bộ hệ thống Nexus. | *Không dùng* (Chờ codebase mới để dọn dẹp) | `Contracts/` cũ & MassTransit DI cũ |

---

## 4. Quy trình làm việc 3 Pha chuẩn Mentor (3-Phase Workflow)
> **Nguyên tắc hành xử:** Toàn bộ quá trình đọc code, tra cứu tài liệu Lead và đối soát contract trong Pha 1 diễn ra **hoàn toàn tự động trong im lặng**. AI tuyệt đối không ngắt quãng bắt User bấm Enter/phê duyệt cho từng file đọc lẻ tẻ. AI chỉ dừng lại để User duyệt ở các chốt chặn quyết định (Plan, Migration, Manual Test).

### Pha 1: Khi nhận Task (Pre-Task Clarification, Git Branching & Guardrail Check)
Trước khi viết bất kỳ dòng code nào, AI bắt buộc phải:
1. **Quy trình Git đầu ca (Pull & Branch - BẮT BUỘC):**
   - **Bắt buộc đồng bộ code mới nhất trước khi code:** `git checkout dev-refactor && git pull origin refactor-all-src` (hoặc `git checkout dev && git pull origin dev` khi nhánh refactor đã chính thức được merge vào `dev`).
   - Tạo nhánh tính năng độc lập khi làm chức năng: `git checkout -b feature/<ma-chuc-nang>-<ten-task>` (ví dụ: `feature/seller-01-registration`, `feature/auth-04-login`).
   - **Quy tắc nhánh `dev-vinh`:** Sau khi hoàn thành và test xong, **Người dùng (Vinh) sẽ tự thực hiện merge nhánh tính năng vào nhánh cá nhân `dev-vinh`**. AI tuyệt đối không tự ý thực hiện lệnh merge.
   - **Tuyệt đối CẤM code hoặc commit trực tiếp trên nhánh `dev` hoặc `main`**.
2. **Khảo sát Banner Hiện trạng & Đọc Specs liên quan:** Soi chiếu các banner `[✅ CÓ TRONG CODE]` và `[❌ CHƯA CÓ]` trong file `specs/` liên quan trực tiếp đến task.
3. **User Story & Mã chức năng:** Nêu rõ vai trò người dùng (`Buyer`, `Seller`, `SupportStaff`, `Admin`), hành động, mục đích, và mã chức năng (ví dụ: `AUTH-04`, `USER-01`, `SELLER-01`...).
4. **Đối chiếu Hợp đồng RESTful:** Dùng kỹ năng `api-contract` đối soát Route, HTTP Method, DTO Request/Response, Mã lỗi HTTP giữa Gateway và UserService.
5. **Kiểm tra Schema & Rào chắn Migration:** Tuyệt đối không sửa file migration cũ. Nếu thiếu cột/bảng, **TUYỆT ĐỐI CẤM tự ý sinh migration mới**! AI bắt buộc phải lập Bản giải trình thay đổi Schema (Schema Change Proposal) nêu rõ lý do tại sao cần để Bạn đem báo cáo cho Team Lead duyệt trước. Chỉ khi Lead đã phê duyệt và Bạn yêu cầu thì mới được sinh migration.
6. **Kế hoạch kiểm chứng (DoD):** `dotnet build User.sln -c Release` (0 lỗi) và chạy test bắn tỉa theo Mục 8.

### Pha 2: Trong quá trình triển khai & Ghi nhận Sự cố (Execution & Troubleshooting)
- Thực hiện code sạch theo thứ tự Clean Architecture: `Domain` $\rightarrow$ `Application` $\rightarrow$ `Infrastructure` $\rightarrow$ `Api`.
- **🚨 QUY TẮC BẮT BUỘC KHI GẶP LỖI LẶP LẠI (Troubleshooting Registry):**
  - Nếu trong quá trình code hoặc chạy test mà bị **fail $\ge 2$ lần liên tiếp**, hoặc gặp lỗi hồi quy (regression):
  - **BẮT BUỘC TẠO NGAY 1 BẢN GHI SỰ CỐ** vào thư mục `D:/obsidian/vinhth/UserService - học/Troubleshooting/` theo mẫu:
    * Triệu chứng lỗi (Error Message, Exception Stack Trace).
    * Nguyên nhân gốc rễ (Root Cause).
    * Cách khắc phục triệt để (Solution / Code Fix).
    * Biện pháp phòng ngừa (Prevention Rule).
  - Cập nhật dòng tóm tắt vào file Master `00_SO_TAY_TRA_CUU_LOI_THUONG_GAP.md` để lần sau gặp lại là có sẵn giải pháp ngay.

### 🛑 CHẶNG DỪNG BẮT BUỘC: USER MANUAL TEST (KHÔNG ĐƯỢC CHẠY GIT ADD/COMMIT)
- Sau khi AI chạy xong kiểm thử tự động (`dotnet test`) đạt 100% pass:
  - **TUYỆT ĐỐI CẤM AI tự ý chạy lệnh `git add`, `git commit` hay `git push`!**
  - AI bắt buộc phải **DỪNG LẠI**, tóm tắt kết quả kiểm thử tự động và bàn giao để **Người dùng (Vinh) tự tay test lại** (bằng Swagger, Postman, gọi API thực tế).
  - **CHỈ KHI NÀO** Người dùng kiểm tra thấy OK và xác nhận (ví dụ: *"test ok rồi, commit đi"*), AI mới được bước sang Pha 3.

### Pha 3: Khi Người dùng đã Test OK & Xác nhận Bàn giao (Git Commit & Obsidian Log)
Chỉ thực hiện sau khi có xác nhận trực tiếp từ Người dùng:
1. **Ghi nhận Task Log:** Tạo file `D:/obsidian/vinhth/UserService - học/Tasks/YYYY-MM-DD_HHmm_[ten-task].md`.
2. **Lệnh Git & Commit Hash (CẤM `git add .`):**
   - Soi chiếu file: `git status`
   - Chỉ add đúng file: `git add <file-cu-the>`
   - Commit chuẩn: `git commit -m "<type>(<scope>): <subject>"`
   - Lấy mã hash: `git log -1 --oneline` (trích xuất mã 7 ký tự điền vào Task Log).
   - Đẩy nhánh tính năng lên remote nếu cần: `git push -u origin feature/<ma-chuc-nang>-<ten-task>`.
   - **Người dùng tự thực hiện merge nhánh vào `dev-vinh`**.
3. **Chiết xuất Khái niệm mới (Concept Extraction):**
   - Tạo file độc lập trong `D:/obsidian/vinhth/UserService - học/Concepts/` theo định dạng `YYYY-MM-DD_[ten-khai-niem].md` để lưu kiến thức lý thuyết phát sinh trong task.

---

## 5. Mẫu File Ghi nhận Sự cố (`Troubleshooting/YYYY-MM-DD_[ten-loi].md`)

````markdown
# 🛠️ SỰ CỐ: [Tên Lỗi / Triệu Chứng Ngắn Gọn]
- **Thời gian ghi nhận:** HH:mm - DD/MM/YYYY
- **Thành phần bị ảnh hưởng:** [Controller / Service / DB Migration / Redis / RabbitMQ / Test]
- **Mức độ nghiêm trọng:** [Cao - Chặn Build/Test | Trung bình | Thấp]

## 1. Triệu chứng & Thông báo lỗi (Error Log)
```text
[Dán log lỗi, exception stacktrace hoặc thông báo dotnet test fail tại đây]
```

## 2. Nguyên nhân gốc rễ (Root Cause)
- [Phân tích tại sao lỗi này xảy ra? Do cấu hình, do bất đồng bộ, do sai kiểu dữ liệu, hay do logic?]

## 3. Cách khắc phục triệt để (Solution)
- **Đoạn code bị lỗi (Before):**
  ```csharp
  // Code lỗi
  ```
- **Đoạn code đã sửa (After):**
  ```csharp
  // Code chuẩn
  ```

## 4. Quy tắc phòng ngừa (Prevention Rule)
- [Quy tắc cần nhớ để lần sau không bao giờ tái phạm lỗi này]
````

---

## 6. Mẫu File Task Log (`Tasks/YYYY-MM-DD_HHmm_[task].md`)

````markdown
# TASK: [Tên Task]
- **Thời gian:** HH:mm - DD/MM/YYYY
- **Branch:** `feature/...`
- **Commit Hash:** `<commit_hash>`
- **Kênh tích hợp:** `[🌐 REST API | 📬 RabbitMQ Event]`
- **Mã chức năng:** `[AUTH-xx / USER-xx / SELLER-xx]`
- **Lệnh xem lại thay đổi:** `git show <commit_hash> --stat`

## 1. User Story & Hợp đồng RESTful
- **User Story:** Là [vai trò], tôi muốn [hành động] để [lợi ích].
- **Endpoint:** `[METHOD] /api/v1/...`
- **Request / Response:**
  ```json
  { ... }
  ```

## 2. Thay đổi trọng tâm (Key Changes)
- **Tạo mới:** `[Đường dẫn file]` - [Mô tả]
- **Chỉnh sửa:** `[Đường dẫn file]`
  - *Trước:* [Đoạn code cũ]
  - *Sau:* [Đoạn code mới]

## 3. Sự cố gặp phải & Cách fix (nếu có $\ge 2$ lần fail)
- Chi tiết xem tại: `[[Troubleshooting/YYYY-MM-DD_[ten-loi]|Xem cách fix]]`

## 4. Lệnh Git đã thực hiện (CẤM git add .)
```bash
git status
git add <duong-dan-file-can-commit>
git commit -m "feat(...): ..."
git log -1 --oneline
```

## 5. Kiểm chứng chất lượng
- `dotnet build User.sln -c Release` (0 lỗi, 0 warning)
- `dotnet test User.sln -c Release` (Pass 100%)
````

---

## 7. Quy Chuẩn An Toàn EF Core Migration (Migration Safety)

### 1. Điều Cấm Kỵ Tuyệt Đối 🚨
* **TUYỆT ĐỐI KHÔNG mở các file `.cs` cũ trong `User.Infrastructure/Persistence/Migrations/` để sửa code bằng tay.**
* **TUYỆT ĐỐI KHÔNG xóa file migration cũ** một khi đã commit vào Git.
* **🚨 TUYỆT ĐỐI CẤM TỰ Ý TẠO MIGRATION MỚI:** Không được phép tự động chạy lệnh `dotnet ef migrations add` khi chưa có sự đồng ý của Team Lead.

### 2. Quy Trình Chuẩn Khi Thay Đổi Cấu Trúc Database
1. **Bước 0 (BẮT BUỘC - Lập Bản Giải Trình Xin Phép Lead):**
   - AI lập bản đề xuất giải trình: *Lý do tại sao tính năng này bắt buộc phải thêm/đổi cột/bảng? Cột tên gì, kiểu dữ liệu gì, có ảnh hưởng gì tới các service khác không?*
   - Người dùng (Vinh) đem báo cáo xin phép Team Lead.
   - **CHỈ TIẾP TỤC KHI LEAD ĐÃ DUYỆT CHÍNH THỨC.**
2. **Bước 1 (Entity & Fluent API):** Sửa Entity trong `User.Domain/Entities/` và cập nhật cấu hình bảng trong `User.Infrastructure/Persistence/Configurations/`.
3. **Bước 2 (Sinh migration mới bằng CLI):**
   ```powershell
   dotnet ef migrations add <TenMigrationMoi> -s User.Api -p User.Infrastructure
   ```
   *Lưu ý: Tên migration phải viết PascalCase mô tả hành động (ví dụ: `AddSellerKycFields`, `AddReviewMediaUrls`).*
4. **Bước 3 (Áp dụng vào CSDL):**
   ```powershell
   dotnet ef database update -s User.Api -p User.Infrastructure
   ```

### 3. Nếu lỡ tạo sai Migration chưa commit lên Git thì làm sao?
Chạy lệnh rollback an toàn để khôi phục ModelSnapshot 100% (tuyệt đối không dùng chuột xóa file):
```powershell
dotnet ef migrations remove -s User.Api -p User.Infrastructure
```

---

## 8. Sổ Tay Lệnh Test Bắn Tỉa Tiết Kiệm Token (Token-Efficient Testing)

* **Nguyên tắc cốt lõi:** Luôn dùng cờ `--logger "console;verbosity=minimal"` để cắt bỏ các dòng pass thừa, chỉ in dòng tóm tắt và stacktrace khi có lỗi.

### Bảng Lệnh Tra Cứu Nhanh:
1. **Chạy duy nhất 1 File Test Controller đang code:**
   ```powershell
   dotnet test User.Tests/User.Tests.csproj --filter "FullyQualifiedName~AuthControllerRestTests" --logger "console;verbosity=minimal"
   ```
2. **Chạy duy nhất 1 Hàm Test đang sửa:**
   ```powershell
   dotnet test User.Tests/User.Tests.csproj --filter "Name=Login_WithValidCredentials_ReturnsTokens" --logger "console;verbosity=minimal"
   ```
3. **Chạy test không Rebuild (`--no-build`) để đạt tốc độ tối đa:**
   ```powershell
   # ⚠️ Điều kiện: Đã build Release thành công trước đó
   dotnet test User.Tests/User.Tests.csproj --no-build -c Release --filter "FullyQualifiedName~AuthControllerRestTests" --logger "console;verbosity=minimal"
   ```
4. **Chạy full solution 1 lần duy nhất trước khi bàn giao (DoD):**
   ```powershell
   dotnet test User.sln -c Release --logger "console;verbosity=minimal"
   ```

