<!-- gitnexus:start -->
# GitNexus — Code Intelligence
This project is indexed by GitNexus as **UserService** (618 symbols, 971 relationships, 16 execution flows). Use the GitNexus MCP tools to understand code, assess impact, and navigate safely.
> Index stale? Run `node .gitnexus/run.cjs analyze` from the project root — it auto-selects an available runner. No `.gitnexus/run.cjs` yet? `npx gitnexus analyze` (npm 11 crash → `npm i -g gitnexus`; #1939).
## Always Do
- **MUST run impact analysis before editing any symbol.** Before modifying a function, class, or method, run `impact({target: "symbolName", direction: "upstream"})` and report the blast radius (direct callers, affected processes, risk level) to the user.
- **MUST run `detect_changes()` before committing** to verify your changes only affect expected symbols and execution flows. For regression review, compare against the default branch: `detect_changes({scope: "compare", base_ref: "main"})`.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- When exploring unfamiliar code, use `query({search_query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `context({name: "symbolName"})`.
- For security review, `explain({target: "fileOrSymbol"})` lists taint findings (source→sink flows; needs `analyze --pdg`).
## Never Do
- NEVER edit a function, class, or method without first running `impact` on it.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis.
- NEVER rename symbols with find-and-replace — use `rename` which understands the call graph.
- NEVER commit changes without running `detect_changes()` to check affected scope.
## Resources
| Resource | Use for |
|----------|---------|
| `gitnexus://repo/UserService/context` | Codebase overview, check index freshness |
| `gitnexus://repo/UserService/clusters` | All functional areas |
| `gitnexus://repo/UserService/processes` | All execution flows |
| `gitnexus://repo/UserService/process/{name}` | Step-by-step execution trace |
## CLI
| Task | Read this skill file |
|------|---------------------|
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus/gitnexus-cli/SKILL.md` |
<!-- gitnexus:end -->
---
# QUY CHUẨN VẬN HÀNH DÀNH CHO AI CODING AGENTS (USER SERVICE)
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — service **có** gRPC server thật (4/13 rpc), **có** 1 migration/17 bảng, **có** 9 unit test. Nhưng ❌ chưa có `Login`/JWT, ❌ 9/13 rpc chưa implement, ❌ 0 consumer RabbitMQ, ❌ 10/14 bảng chưa dùng, ❌ 0 test gRPC.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
---
## 🛑 NGUYÊN TẮC 1: BẮT BUỘC LẬP KẾ HOẠCH TRƯỚC KHI HÀNH ĐỘNG (PLAN-FIRST RULE)
Mọi AI Agent khi nhận nhiệm vụ kỹ thuật tại UserService **BẮT BUỘC PHẢI LẬP VÀ TRÌNH BÀY PLAN** trước khi thực hiện bất kỳ hành động sửa đổi mã nguồn, thêm/xóa file, refactor, tạo migration database hay thay đổi cấu hình nào.
Một Plan chuẩn bắt buộc phải có:
1. **Mục tiêu (Objective)**: Tóm tắt chính xác điều cần làm.
2. **Context & Files cần đọc**: Liệt kê các file nội bộ của UserService cần đọc theo đúng ma trận đọc file tại Nguyên tắc 3.
3. **Phân tích rủi ro & Phạm vi ảnh hưởng (Impact Analysis)**:
   - Dùng GitNexus `impact` đánh giá blast radius các symbol bị ảnh hưởng.
   - Cảnh báo rõ nếu mức độ rủi ro là HIGH hoặc CRITICAL.
4. **Checklist các bước thực hiện (Step-by-Step Implementation)**: Từng bước logic, chi tiết.
5. **Kế hoạch kiểm chứng (Verification)**: `dotnet build` và `dotnet test` tương ứng.
---
## 🧭 NGUYÊN TẮC 2: QUY TẮC PHẠM VI NỘI BỘ (INWARD-ONLY / SELF-CONTAINED)
UserService là một Git Repository độc lập (`src/Services/User`):
- **TUYỆT ĐỐI CHỈ ĐƯỢC trỏ tới các tài nguyên nội bộ của UserService**.
- **TUYỆT ĐỐI KHÔNG ĐƯỢC trỏ ngược ra ngoài Root** (`../../../docs/...`) hay sang các Service khác (`../../Catalog/...`).
- Toàn bộ tài liệu, hướng dẫn và cấu hình trong repo này phải 100% tự cung tự cấp (Self-contained).
---
## 📚 NGUYÊN TẮC 3: DANH MỤC FILE NỘI BỘ BẮT BUỘC PHẢI ĐỌC TẠI USER SERVICE
Khi bắt đầu làm việc tại UserService, AI Agent **BẮT BUỘC PHẢI ĐỌC** các file nội bộ sau theo thứ tự:
1. `specs/00_AGENT_MISSION_GUARDRAILS.md`: Bounded context quản lý định danh, port `8001`, DB `usermanagement_db` (host `5433` / trong Docker `user-db:5432`).
2. `specs/01_CLEAN_ARCHITECTURE_LAYERS.md`: Cấu trúc 4 tầng Clean Architecture (5 project), No-Repository pattern over EF Core, 4 vi phạm phân tầng đã biết.
3. `specs/02_API_GRPC_CONTRACTS.md`: Hợp đồng gRPC Server `UsersApi` — **13 rpc khai báo, 4 rpc đã implement** (Register, VerifyOtp, ResendOtp, GetProfile); bảng map mã lỗi → gRPC status thật.
4. `specs/03_EVENT_MESSAGING.md`: Sự kiện RabbitMQ — **duy nhất** `SendOtpEmailIntegrationEvent` (2 điểm publish, 0 consumer); MassTransit 8.3.0 + EF Outbox.
5. `specs/04_DATA_PERSISTENCE.md`: Schema PostgreSQL — **14 entity/DbSet**, migration tạo **17 bảng**, 10/14 bảng chưa có truy vấn nghiệp vụ.
6. `specs/05_DEV_VERIFICATION_CHECKLIST.md`: Lệnh build & test kiểm chứng service (9 `[Fact]`).
---
## 🔒 NGUYÊN TẮC 4: RÀO CHẮN BẤT BIẾN TẠI USER SERVICE
1. **TUYỆT ĐỐI KHÔNG DÙNG REPOSITORY PATTERN BỌC NGOÀI EF CORE**: Sử dụng trực tiếp `AppDbContext` (lớp triển khai thật của `IUserDbContext`) và `DbSet<T>` trong tầng Application. Cấm tạo `IUserRepository`, `GenericRepository`, `UnitOfWork`. ⚠️ Lưu ý: `UserDbContext : AppDbContext` tồn tại trong code nhưng **không được đăng ký DI** — đừng dùng nó.
2. **TUYỆT ĐỐI KHÔNG LƯU MẬT KHẨU PLAIN TEXT**: Mật khẩu phải được hash bằng **BCrypt có salt** (`BCrypt.Net-Next 4.0.3`, `AuthService.cs:75,95`). ⚠️ Repo **không** dùng Argon2id — đừng viết tài liệu/code giả định có Argon2.
3. **CẤU HÌNH PORT 8001 (gRPC + HTTP cùng port, h2c) VÀ DATABASE `usermanagement_db` (host port 5433) LÀ BẤT BIẾN**. Tuyệt đối không dùng tên `user_db`.
4. **HỢP ĐỒNG PROTOBUF ĐỒNG BỘ 100% VỚI GATEWAY**: `User.Api/Contracts/shop.proto` là bản server-side; **cấm sửa** khi chưa chốt nguồn sự thật Protobuf toàn hệ thống.
5. **TÔN TRỌNG HIỆN TRẠNG**: không mô tả tính năng chưa có (JWT, refresh token, seller, report, penalty, audit log) như thể đã hoạt động. Xem Nguyên tắc 7.
---
## 💬 NGUYÊN TẮC 5: QUY CHUẨN THÔNG BÁO LỖI TOÀN HỆ THỐNG (MANDATORY ERROR RULES)
Tất cả các thông báo lỗi (Error Messages, Exceptions, ProblemDetails, Validation Errors, Response Details) **BẮT BUỘC** phải tuân theo 3 nguyên tắc sau:
1. **Ngôn ngữ (100% English)**: Toàn bộ thông báo lỗi phải được viết bằng tiếng Anh. Tuyệt đối không dùng tiếng Việt trong chuỗi thông báo lỗi trả về cho client.
2. **Quy cách viết hoa (Sentence case)**: Viết hoa duy nhất chữ cái đầu câu, các từ tiếp theo viết thường (ngoại trừ tên riêng, từ viết tắt hoặc định danh kỹ thuật như `ID`, `OTP`, `JWT`, `URL`, `UUID`, `Email`...).
   - ✅ Đúng: `"Email is already registered in the system."`
   - ✅ Đúng: `"OTP code has expired or does not exist."`
   - ✅ Đúng: `"OTP code is invalid."`
   - ✅ Đúng: `"This OTP code has already been used."`
   - ✅ Đúng: `"Username, valid email, and password with at least 8 characters are required."`
   - ❌ Sai: `"Lỗi mã OTP không hợp lệ"` (Không dùng tiếng Việt)
   - ❌ Sai: `"invalid email address"` (Chữ cái đầu không viết hoa)
   - ❌ Sai: `"INVALID EMAIL ADDRESS"` (Cấm viết hoa toàn bộ)
   - ❌ Sai: `"Invalid Email Address Provided"` (Không dùng Title Case)
3. **Mã lỗi (Error Codes — SCREAMING_SNAKE_CASE)**: Mã lỗi luôn viết hoa toàn bộ và nối bằng dấu gạch dưới:
   - Mã **đang có thật** trong `AuthService`: `EMAIL_ALREADY_EXISTS`, `OTP_EXPIRED`, `OTP_INVALID`, `OTP_ALREADY_USED`, `USER_NOT_FOUND`, `USER_ALREADY_VERIFIED`, `VAL_INVALID_EMAIL`, `VAL_INVALID_PASSWORD`, `VAL_INVALID_FIRST_NAME`, `VAL_INVALID_LAST_NAME`, `VAL_INVALID_OTP`.
   - ⚠️ `UNAUTHORIZED` / `FORBIDDEN` / `VAL_INVALID_INPUT` **chưa được dùng** ở đâu trong code (thuộc đặc tả tương lai).
---
## 🎯 NGUYÊN TẮC 6: ĐIỀU KIỆN HOÀN THÀNH NHIỆM VỤ (DEFINITION OF DONE)
Một task tại UserService chỉ hoàn tất khi:
- [ ] Đã lập Plan chi tiết trước khi thực hiện.
- [ ] Không chứa bất kỳ liên kết trỏ ngược ra ngoài UserService.
- [ ] `dotnet build User.sln -c Release` thành công 100% (0 error) — file solution là **`User.sln`**, không phải `UserService.sln`.
- [ ] `dotnet test User.sln -c Release` vượt qua 100% các bài test (**9** test hiện có).
- [ ] Trạng thái Git sạch, không commit file rác (`bin/`, `obj/`, `.vs/`, `.idea/`, `.env`).
---
## 🧪 NGUYÊN TẮC 7: HIỆN TRẠNG ĐÃ KIỂM CHỨNG (BẮT BUỘC ĐỌC TRƯỚC KHI CODE)
> Số liệu đo bằng cách đọc toàn bộ **65 file `.cs` / 2.433 dòng** (không tính `Migrations/`) trong repo này, ngày **2026-10-24**. Đây là sự thật đã kiểm chứng, không phải mô tả kiến trúc mong muốn.
| Hạng mục | Hiện trạng thật |
|:---|:---|
| Dự án | 5 project: `User.Domain` 34 file/520 LOC · `User.Application` 8/374 · `User.Infrastructure` 19/949 · `User.Api` 3/324 · `User.Tests` 1/266 |
| Bề mặt gRPC | **4/13 rpc** (`Register`, `VerifyOtp`, `ResendOtp`, `GetProfile`); 9 rpc còn lại trả `Unimplemented` |
| Auth | ❌ Không có `Login`, ❌ không có code phát JWT (grep `Jwt|GenerateToken|SecurityToken` = 0); `appsettings.json` có section `Jwt` nhưng **không ai đọc** |
| OTP | Sinh bằng `Random.Shared.Next(100000, 999999)` (**không CSPRNG**), lưu Redis TTL 5 phút, **không rate-limit**; OTP thật bị ghi vào log khi bus offline (`AuthService.cs:157`) |
| Event bus | **1 publisher** (`SendOtpEmailIntegrationEvent`), **0 consumer**, MassTransit **8.3.0** + EF Outbox; publish đang gọi **sau** `SaveChangesAsync` (rủi ro mất message) |
| Dữ liệu | 14 entity/DbSet, migration **1 file** tạo **17 bảng**; chỉ **4/14 DbSet** có truy vấn nghiệp vụ; `HasQueryFilter` phủ **8/14** entity |
| Phân tầng | ⚠️ Vi phạm: `User.Api` → `User.Infrastructure` và `UsersGrpcService` inject thẳng `AppDbContext` |
| Test | **9** `[Fact]` trong `User.Tests/AuthServiceTests.cs`; `User.Tests` **không** reference `User.Api` ⇒ 0 test gRPC |
| Bảo mật | Không có authentication/authorization trong pipeline; tin header `x-user-id` do client tự khai |
**Quy tắc rút ra:**
- Không được coi `/health` là bằng chứng "service hoạt động" — nó **không** kiểm tra DB.
- Mọi tài liệu khi sửa **phải có banner trạng thái** (✅ CÓ / ⚠️ MỘT PHẦN / ❌ CHƯA CÓ + nguồn sự thật + ngày) theo.
- Trước khi thêm tính năng, kiểm tra bảng "Hiện trạng" ở trên: phần lớn schema (seller/report/penalty/audit/refresh token) **đã có bảng nhưng chưa có code** — không được giả định là đã chạy.