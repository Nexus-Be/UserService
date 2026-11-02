# RÀO CHẮN BẤT BIẾN CODEBASE & AN TOÀN HỆ THỐNG (CODEBASE GUARDRAILS)
> **Phạm vi:** Bắt buộc áp dụng cho mọi AI Agent hoạt động tại `UserService`.  
> **Nguồn sự thật:** `AGENTS.md` (UserService & Gateway).

---

## 1. RÀO CHẮN AN TOÀN DATABASE & MIGRATION 🔒
* **TUYỆT ĐỐI CẤM SỬA TAY FILE MIGRATION CŨ:** Không được mở bất kỳ file nào trong `User.Infrastructure/Persistence/Migrations/` để sửa đổi code.
* **🚨 CẤM TỰ Ý TẠO MIGRATION MỚI (BẮT BUỘC BÁO CÁO GIẢI TRÌNH CHO TEAM LEAD DUYỆT):**
  * Developer và AI **TUYỆT ĐỐI KHÔNG ĐƯỢC TỰ Ý TẠO MIGRATION MỚI**.
  * Khi tính năng phát sinh nhu cầu thêm bảng, thêm cột hoặc đổi schema:
    1. AI bắt buộc phải lập **Bản Giải Trình Thay Đổi Schema (Schema Change Proposal)** nêu rõ: *Lý do tại sao cần? Cột/bảng gì? Kiểu dữ liệu gì? Rủi ro ảnh hưởng thế nào?*
    2. Bàn giao bản giải trình để Người dùng (Vinh) đem **báo cáo và xin phê duyệt từ Team Lead**.
    3. **CHỈ KHI NÀO** Team Lead đã phê duyệt và Người dùng trực tiếp ra lệnh (ví dụ: *"Lead đã duyệt, tạo migration đi"*), AI mới được phép hỗ trợ sinh migration mới.
* **Quy trình khi đã được Lead duyệt:**
  1. Cập nhật Entity trong `User.Domain`.
  2. Cấu hình Fluent API trong `User.Infrastructure/Persistence/Configurations/`.
  3. Dùng lệnh CLI sinh migration mới độc lập: `dotnet ef migrations add <TenMoi> -s User.Api -p User.Infrastructure`.
* **Tên Database chuẩn:** **`user_db`** (Cổng nội bộ `5432`, cổng ánh xạ máy host `5433`).

---

## 2. RÀO CHẮN BẢO MẬT & SECRET (MANDATORY HUMAN-IN-THE-LOOP) 🔐
* **CẤM TỰ Ý SỬA ĐỔI BIẾN MÔI TRƯỜNG & SECRET:** Tuyệt đối cấm AI tự ý sửa đổi, ghi đè, xóa các file `.env`, connection string, API key, JWT secret, chứng chỉ bảo mật.
* Bắt buộc phải có **sự đồng ý rõ ràng (explicit approval)** từ người dùng trước khi chạm vào bất kỳ cấu hình bảo mật nào.

---

## 3. RÀO CHẮN KIẾN TRÚC & GIAO THỨC MẠNG 🌐
* **BẮT BUỘC 100% RESTFUL HTTP/JSON:** Toàn bộ API của UserService (cho cả Gateway và các microservice nội bộ gọi nhau) bắt buộc dùng chuẩn **RESTful Web API** (`/api/v1/auth`, `/api/v1/users`, `/api/v1/internal/*`).
* **TUYỆT ĐỐI CẤM gRPC CHO GIAO TIẾP ĐỒNG BỘ:** Không tạo file `.proto`, không implement `GrpcService`.
* **TUYỆT ĐỐI KHÔNG DÙNG REPOSITORY PATTERN BỌC NGOÀI EF CORE:** Sử dụng trực tiếp `AppDbContext` và `DbSet<T>` trong tầng Application. Cấm tạo `IUserRepository`, `GenericRepository`, `UnitOfWork`.
* **MẬT KHẨU BẮT BUỘC BĂM BẰNG BCRYPT CÓ SALT:** Sử dụng `BCrypt.Net-Next 4.0.3`. Không dùng plain text, không dùng Argon2id (dự án chưa có package này).

---

## 4. QUY CHUẨN THÔNG BÁO LỖI TOÀN HỆ THỐNG 💬
* **Ngôn ngữ 100% tiếng Anh:** Tuyệt đối không dùng tiếng Việt trong chuỗi thông báo lỗi trả về cho client.
* **Sentence case:** Viết hoa chữ cái đầu câu, các từ sau viết thường (ngoại trừ tên riêng, viết tắt kỹ thuật như `ID`, `OTP`, `JWT`, `Email`):
  * ✅ Đúng: `"Email is already registered in the system."`
  * ❌ Sai: `"Lỗi email đã tồn tại"`, `"invalid email"`, `"INVALID EMAIL"`.
* **Mã lỗi `SCREAMING_SNAKE_CASE`:** `EMAIL_ALREADY_EXISTS`, `OTP_INVALID`, `VAL_INVALID_INPUT`, `USER_NOT_FOUND`.

---

## 5. RÀO CHẮN BÃI BỎ MESSAGE BROKER & GIAO TIẾP LIÊN-SERVICE 100% RESTFUL 🌐
* **BÃI BỎ HOÀN TOÀN RABBITMQ & MASSTRANSIT:** Toàn bộ cơ chế Message Broker, MassTransit và các bảng Outbox/Inbox (`OutboxMessage`, `OutboxState`, `InboxState`) đã bị bãi bỏ hoàn toàn khỏi UserService. Tuyệt đối không thêm mới bất kỳ package broker nào.
* **GIAO TIẾP LIÊN-SERVICE QUA TYPED REST CLIENT:** 
  - Mọi tương tác đồng bộ giữa các microservices (như gửi email OTP sang `NotificationService`) bắt buộc triển khai qua Typed REST Client (`IEmailNotificationClient`) bằng `HttpClientFactory` kèm cấu hình Timeout và Retry an toàn.
  - Không làm gián đoạn transaction chính của UserService nếu service đối tác tạm thời gặp sự cố.
* **AN TOÀN BẢO MẬT & NGOẠI LỆ OTP:** Không ghi mã OTP hoặc mật khẩu plain-text ra log môi trường production.
* **CODE MẪU & HƯỚNG DẪN:** Kích hoạt skill `@outbox-event` khi cần tham chiếu mẫu code HttpClient liên-service hoặc xử lý tác vụ nền nội bộ (In-Memory Channels).

---

## 6. RÀO CHẮN BẢO VỆ TÀI NGUYÊN BÊN NGOÀI & SPECS: CHỈ ĐƯỢC PHÉP ĐỌC — TUYỆT ĐỐI CẤM CHỈNH SỬA (STRICT READ-ONLY RESOURCES) 🔒📖

* **DANH SÁCH TÀI NGUYÊN BẤT BIẾN CHỈ ĐỌC (READ-ONLY 100%):**
  1. **Thư mục `specs/` (Nội bộ UserService):**
     - Là **Đặc tả kiến trúc chuẩn (Read-Only Specification)** của cả dự án.
     - **TUYỆT ĐỐI CẤM** tự ý sửa đổi, viết lại, chèn nội dung thảo luận hoặc "fix" bất kỳ file nào trong `specs/`.
     - **Quy chuẩn ghi chú:** Nếu phát sinh nội dung thảo luận mới, checklist mở rộng hoặc đề xuất thay đổi, **BẮT BUỘC PHẢI TẠO FILE NOTE RIÊNG** bên ngoài (trong Obsidian `Tasks/`) để Developer (Vinh) tự tay xem xét và cập nhật sau.
  2. **Thư mục Gateway (`C:\Users\ACER\Desktop\Gateway`):**
     - **CHỈ ĐƯỢC PHÉP TRỎ ĐỌC** để đối soát cấu hình Route YARP, Header Propagation, Anti-spoofing và Response Envelope.
     - **TUYỆT ĐỐI CẤM CHỈNH SỬA** bất kỳ file mã nguồn hay cấu hình nào bên Gateway khi đang làm việc tại UserService.
  3. **Thư mục `root`:**
     - **CHỈ ĐƯỢC PHÉP TRỎ ĐỌC** để tham chiếu cấu hình chung toàn hệ thống, docker-compose tổng thể. Tuyệt đối cấm sửa.
  4. **Thư mục `obsidian` (Tài liệu nghiệp vụ & Kiến trúc):**
     - **CHỈ ĐƯỢC PHÉP TRỎ ĐỌC** toàn bộ tài liệu đặc tả chung: `BE/`, `FE/`, `User Service Lead/`, `00_MASTER_FLOW_TOAN_BO_HE_THONG_THEO_PHASE.md`, v.v.
     - **TUYỆT ĐỐI CẤM SỬA** các file tài liệu chuẩn của nhóm.
     - *Ngoại lệ duy nhất được ghi:* Chỉ được phép tạo file Task Log (`Tasks/`) hoặc Sổ tay sự cố (`Troubleshooting/`) trong thư mục cá nhân `UserService - học/` khi hoàn thành task hoặc gặp lỗi lặp lại.
  5. **Thư mục `Nexus`:**
     - **CHỈ ĐƯỢC PHÉP TRỎ ĐỌC** để đối chiếu giao tiếp với các service khác (`Catalog`, `Auction`, `Order`, `Wallet`...). Tuyệt đối cấm sửa mã nguồn của các service này.
  6. **File quy chuẩn nhóm (`AGENTS.md`, `CLAUDE.md`):**
     - Được Git của nhóm theo dõi, **TUYỆT ĐỐI CẤM SỬA**. Mọi rule tùy biến và skill cá nhân chỉ được đặt trong `.agents/` (đã gitignored).

* **THỨ TỰ ƯU TIÊN NGUỒN SỰ THẬT (`specs/` THẮNG):**
  * **Cấp 1 (Tối cao):** Thư mục `specs/` trong repo `UserService` là **Nguồn sự thật tối cao** về hiện trạng kỹ thuật, port, schema CSDL và rào chắn triển khai.
  * **Cấp 2 (Nghiệp vụ bổ trợ):** Kho `User Service Lead` cung cấp User Stories, ma trận quyền và phân tích chi tiết.
  * **Quy tắc giải quyết mâu thuẫn:** `specs/` **luôn thắng**. Nếu phát hiện điểm mâu thuẫn giữa `specs/` và `User Service Lead`, AI **BẮT BUỘC PHẢI BÁO CÁO** cho User/Mentor, tuyệt đối không tự ý suy đoán hay tự tiện chọn một bên.
* **ĐỌC ĐÚNG FILE SPECS LIÊN QUAN TRỰC TIẾP TỚI TASK:** Chỉ đọc các file specs liên quan đến tính năng đang làm (ví dụ: làm Auth đọc `specs/02`, làm CSDL đọc `specs/04`, làm Event đọc `specs/03`), **KHÔNG** đọc tràn lan cả 6 file cùng lúc để tiết kiệm token.

---

## 7. RÀO CHẮN LỆNH PHÁ HỦY & AN TOÀN GIT (DESTRUCTIVE COMMAND GUARDRAILS) 🛑
* **TUYỆT ĐỐI CẤM CÁC LỆNH PHÁ HỦY DỮ LIỆU & LỊCH SỬ:**
  * ❌ `git push --force`, `git push -f`
  * ❌ `git reset --hard` (làm mất vĩnh viễn code chưa commit)
  * ❌ `dotnet ef database drop` (xóa sạch CSDL `user_db`)
  * ❌ Lệnh xóa file hàng loạt: `rm -rf`, `Remove-Item * -Recurse -Force`
* **CẤM `git add .` MÙ QUÁNG:** Tuyệt đối không dùng `git add .` vì có thể vô tình stage các file rác, file `.env`, file `bin/obj` hoặc các file chung của nhóm (`AGENTS.md`, `specs/`). Bắt buộc chạy `git status` trước, sau đó chỉ dùng `git add <duong-dan-file-cu-the>`.
* **🚨 CẤM TỰ Ý `git add` / `git commit` SAU KHI TEST AUTOMATION (BẮT BUỘC CHỜ USER TEST LẠI):**
  * Sau khi AI chạy xong kiểm thử tự động (`dotnet build`, `dotnet test`), **TUYỆT ĐỐI CẤM AI TỰ Ý CHẠY BẤT KỲ LỆNH `git add` HOẶC `git commit` NÀO**.
  * AI bắt buộc phải **DỪNG LẠI**, thông báo rõ ràng để Người dùng (Vinh) tự tay test lại (Manual test, Swagger, Postman).
  * **CHỈ KHI NÀO** Người dùng kiểm tra thấy OK và trực tiếp ra lệnh (ví dụ: *"test ok rồi, commit đi"*), AI mới được phép hỗ trợ bước Git.
* **QUY CHUẨN NHÁNH `dev` VÀ `dev-refactor`:**
  * **Trước khi code:** Bắt buộc pull mã nguồn mới nhất từ nhánh chung: `git pull origin dev`.
  * **Tạo branch & Merge:** Nhánh tính năng được tạo khi làm chức năng, sau đó **Người dùng sẽ tự merge vào nhánh `dev-refactor`**. AI tuyệt đối không tự ý thực hiện lệnh merge.

---

## 8. NGUYÊN TẮC: TỰ CHỦ ĐỌC NGHIÊN CỨU — PHÊ DUYỆT KHI SỬA ĐỔI (READ-AUTONOMOUS, WRITE-GATEKEEPER) 🚀
* **VÙNG TỰ CHỦ 100% (CẤM LÀM PHIỀN USER):**
  * Khi nghiên cứu, đọc code, đọc specs, tra cứu tài liệu Lead, đối soát API contract: AI **phải tự động đọc một mạch liên tục trong im lặng**.
  * Ưu tiên công cụ đọc file trực tiếp (`read_file`) và chế độ Sandbox chuẩn.
  * **TUYỆT ĐỐI KHÔNG ngắt quãng** bắt User phải bấm Enter hay xác nhận cho từng file đọc lẻ tẻ.
* **VÙNG PHÊ DUYỆT BẮT BUỘC (HUMAN-IN-THE-LOOP - 3 CHẶNG KIỂM SOÁT):**
  AI chỉ được phép dừng lại yêu cầu User duyệt ở đúng 3 chặng sau:
  1. **Chặng 1 (Plan-First):** Trình bày Kế hoạch (Plan) hoàn chỉnh $\rightarrow$ User duyệt mới được sửa code.
  2. **Chặng 2 (DB Schema):** Trình bày Bản Giải Trình Migration $\rightarrow$ User đem báo cáo Team Lead duyệt.
  3. **Chặng 3 (Verification):** AI test automation xong $\rightarrow$ Bàn giao để User tự test manual. User test thấy OK thì mới tiến hành bước Git commit/merge.

---

## 9. NGUYÊN TẮC VÀNG: CẨN TRỌNG TUYỆT ĐỐI — CHẤT LƯỢNG TRÊN TỐC ĐỘ (PRECISION OVER SPEED) 🛡️
* **TƯ DUY KỸ SƯ CHUẨN MỰC:** "Chậm mà chắc, làm đâu trúng đó". Tuyệt đối không làm nhanh làm ẩu để chạy tiến độ. Thà dành thời gian nghiên cứu kỹ lưỡng còn hơn làm sai sinh lỗi phá vỡ hệ thống.
* **TUYỆT ĐỐI CẤM SUY ĐOÁN & MƠ HỒ (ZERO GUESSWORK):**
  * Không được giả định bất kỳ điều gì. Mọi thông tin về logic, trường dữ liệu, mã lỗi đều phải có trích dẫn xuất xứ cụ thể từ Specs, tài liệu Lead hoặc mã nguồn (`file:line`).
* **ĐỐI SOÁT ĐA CHIỀU 360 ĐỘ TRƯỚC KHI LẬP PLAN:**
  * Bắt buộc kiểm tra 4 góc: *Specs Lead $\leftrightarrow$ Controller Gateway $\leftrightarrow$ Entity Database $\leftrightarrow$ Edge Cases (các nhánh lỗi biên).*
  * Luôn bao quát các trường hợp ngoại lệ: nhập sai quá số lần cho phép, trùng lặp dữ liệu, tài khoản bị khóa, timeout Redis/RabbitMQ.
* **PHÂN TÍCH RỦI RO (BLAST RADIUS) BẰNG GITNEXUS:**
  * Trước khi chỉnh sửa hàm hoặc class dùng chung, bắt buộc dùng GitNexus để đánh giá phạm vi ảnh hưởng (những ai đang gọi hàm này), đảm bảo không gây lỗi dây chuyền sang các module khác.
