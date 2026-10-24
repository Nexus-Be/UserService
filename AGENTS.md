<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **UserService** (617 symbols, 971 relationships, 16 execution flows).

> Index stale? Run `node .gitnexus/run.cjs analyze --index-only` from the project root — it auto-selects an available runner. No `.gitnexus/run.cjs` yet? Bootstrap with `npx`, `bunx`, or `pnpm dlx` — e.g. `bunx gitnexus@latest analyze` (npm 11 npx crash; #1939).

## Always Do

- **MUST run impact before editing.** Use `impact({target: "symbolName", direction: "upstream"})` or `node .gitnexus/run.cjs impact "symbolName" --direction upstream --repo .`; report callers, processes, and risk. Never substitute grep for graph analysis.
- **MUST analyze graph changes before committing.** Use `detect_changes({scope: "all"})` (MCP) or `node .gitnexus/run.cjs detect-changes --scope all --repo .` (CLI fallback). `partial: true` or `truncated: true` is not a clean check — a zero means unseen, not unaffected; re-run it. For regression review: `detect_changes({scope: "compare", base_ref: "main"})` or `node .gitnexus/run.cjs detect-changes --scope compare --base-ref "main" --repo .`.
- MUST warn on HIGH/CRITICAL `risk` pre-edit; never use `riskSharedAxes` to waive a HIGH/CRITICAL `risk` warning. Compare File/symbol: MCP File omits axes; Graph-RAG expands File.
- **MUST treat `risk: UNKNOWN` as unresolved, not as low.** An empty caller set is not evidence the symbol is unused — it can also mean the callers are not resolvable by the index (plain-object property access, dynamic dispatch, cross-language calls). `impact` pairs `UNKNOWN` with a `riskNote` saying so. Confirm with a text search before treating the symbol as safe to change or delete; do not proceed on the strength of a zero.
- **MUST use `query({search_query: "concept"})` for concepts/flows, `context({name: "symbolName"})` for a named symbol, or `impact` for blast radius, on read-only callers, dependencies, imports, or execution flow.** Graph first; text search only for empty/`UNKNOWN`/literals.
- For security review, `explain({target: "fileOrSymbol"})` lists taint findings (source→sink flows; needs `analyze --pdg`).

## Never Do

- NEVER edit a function, class, or method before MCP/CLI impact analysis.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis, and never read `UNKNOWN` as an all-clear — it means the walk could not answer, which is the one verdict that requires confirming by other means.
- NEVER rename symbols with find-and-replace — use `rename` which understands the call graph.
- NEVER commit before MCP/CLI graph change analysis.

## Resources

| Resource | Use for |
| --- | --- |
| `gitnexus://repo/UserService/context` | Codebase overview, check index freshness |
| `gitnexus://repo/UserService/clusters` | All functional areas |
| `gitnexus://repo/UserService/processes` | All execution flows |
| `gitnexus://repo/UserService/process/{name}` | Step-by-step execution trace |

## CLI

| Task | Read this skill file |
| --- | --- |
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->

---

# QUY CHUẨN VẬN HÀNH DÀNH CHO AI CODING AGENTS (USER SERVICE)

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

UserService là một Git Repository độc lập (`UserService.git`):
- **TUYỆT ĐỐI CHỈ ĐƯỢC trỏ tới các tài nguyên nội bộ của UserService**.
- **TUYỆT ĐỐI KHÔNG ĐƯỢC trỏ ngược ra ngoài Root** (`../../../docs/...`) hay sang các Service khác (`../../Catalog/...`).
- Toàn bộ tài liệu, hướng dẫn và cấu hình trong repo này phải 100% tự cung tự cấp (Self-contained).

---

## 📚 NGUYÊN TẮC 3: DANH MỤC FILE NỘI BỘ BẮT BUỘC PHẢI ĐỌC TẠI USER SERVICE

Khi bắt đầu làm việc tại UserService, AI Agent **BẮT BUỘC PHẢI ĐỌC** các file nội bộ sau theo thứ tự:
1. `specs/00_AGENT_MISSION_GUARDRAILS.md`: Bounded context quản lý định danh, port `8001`, DB `5433` (`usermanagement_db`).
2. `specs/01_CLEAN_ARCHITECTURE_LAYERS.md`: Cấu trúc 4 tầng Clean Architecture, No-Repository pattern over EF Core.
3. `specs/02_API_GRPC_CONTRACTS.md`: Hợp đồng gRPC Server (`UsersApi`: Register, Login, VerifyOtp, GetProfile...).
4. `specs/03_EVENT_MESSAGING.md`: Sự kiện RabbitMQ (`UserRegisteredEvent`, `UserVerifiedEvent`...).
5. `specs/04_DATA_PERSISTENCE.md`: Schema PostgreSQL (`Users`, `Roles`, `Permissions`, `SellerRegistrations`).
6. `specs/05_DEV_VERIFICATION_CHECKLIST.md`: Lệnh build & test kiểm chứng service.

---

## 🔒 NGUYÊN TẮC 4: RÀO CHẮN BẤT BIẾN TẠI USER SERVICE

1. **TUYỆT ĐỐI KHÔNG DÙNG REPOSITORY PATTERN BỌC NGOÀI EF CORE**: Sử dụng trực tiếp `UserDbContext` và `DbSet<T>` (hoặc interface `IUserDbContext`) trong tầng Application. Cấm tạo `IUserRepository`, `GenericRepository`, `UnitOfWork`.
2. **TUYỆT ĐỐI KHÔNG LƯU MẬT KHẨU PLAIN TEXT**: Mật khẩu phải được hash bằng Argon2id hoặc BCrypt có salt ngẫu nhiên.
3. **CẤU HÌNH PORT 8001 VÀ DATABASE 5433 LÀ BẤT BIẾN**.
4. **HỢP ĐỒNG PROTOBUF ĐỒNG BỘ 100% VỚI GATEWAY**.

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
   - Ví dụ: `EMAIL_ALREADY_EXISTS`, `OTP_EXPIRED`, `OTP_INVALID`, `OTP_ALREADY_USED`, `USER_NOT_FOUND`, `VAL_INVALID_INPUT`, `UNAUTHORIZED`, `FORBIDDEN`.

---

## 🎯 NGUYÊN TẮC 6: ĐIỀU KIỆN HOÀN THÀNH NHIỆM VỤ (DEFINITION OF DONE)

Một task tại UserService chỉ hoàn tất khi:
- [ ] Đã lập Plan chi tiết trước khi thực hiện.
- [ ] Không chứa bất kỳ liên kết trỏ ngược ra ngoài UserService.
- [ ] `dotnet build UserService.sln -c Release` thành công 100% (0 error).
- [ ] `dotnet test UserService.sln -c Release` vượt qua 100% các bài test.
- [ ] Trạng thái Git sạch, không commit file rác (`bin/`, `obj/`, `.vs/`, `.idea/`).

---

## 📝 NGUYÊN TẮC 7: QUY CHUẨN DEV-JOURNAL & MENTOR WORKFLOW (OBSIDIAN VAULT)

Để phục vụ việc học tập, quản lý tiến độ và review code cùng Mentor, AI Agent bắt buộc phải tuân theo kỹ năng `.agents/skills/dev-mentor-workflow`:
1. **Tuyệt đối không lưu file `.md` ghi chú bừa bãi vào source code dự án**:
   - Toàn bộ nhật ký thực chiến lưu tại: `D:/obsidian/vinhth/UserService/Tasks/`
   - Toàn bộ sổ tay lý thuyết & khái niệm lưu tại: `D:/obsidian/vinhth/UserService/Concepts/`
2. **Quy trình 2 Pha bắt buộc khi thực hiện Task**:
   - **Pha 1 (Bắt đầu):** Xác định rõ **User Story** và **Câu hỏi làm rõ** trước khi đụng vào code.
   - **Pha 2 (Kết thúc):** 
     - Tóm tắt thay đổi code trọng tâm (File tạo mới, đoạn code cốt lõi trước/sau).
     - Hướng dẫn lệnh Git (Branch, add, commit message chuẩn).
     - Trích xuất mã **Commit Hash** (`git log -1 --oneline`) điền vào file Task Log để Mentor dễ dàng review bằng lệnh `git show <commit_hash>`.
   - **Lý thuyết phát sinh:** Tự động tách riêng thành file tài liệu độc lập trong `Concepts/` kèm mốc thời gian (ngày, tháng, năm, giờ, phút) để hiển thị trong Obsidian Vault.

