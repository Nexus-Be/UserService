# 05 — DANH SÁCH KIỂM CHỨNG & NGHIỆM THU (USER SERVICE CHECKLIST)
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — lệnh build/test **chạy được** (5 project, solution `User.sln`), **9/9 `[Fact]`** thuộc `AuthServiceTests`. ❌ **0 test** cho tầng gRPC (`User.Tests` không reference `User.Api`), ❌ **0 test** cho Redis/MassTransit (không có Testcontainers, không có mock `IPublishEndpoint`).
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
---
## 1. LỆNH BIÊN DỊCH & CHẠY THỬ NGHIỆM
```bash
# 1. Biên dịch toàn bộ solution (file solution tên là User.sln, KHÔNG phải UserService.sln)
dotnet build User.sln -c Release
# 2. Chạy toàn bộ 9 unit test (xunit)
dotnet test User.sln -c Release
# 3. Chạy service cục bộ (gRPC + HTTP cùng port 8001, h2c)
dotnet run --project User.Api
# 4. Kiểm tra service sống (HTTP/1.1)
curl -i http://localhost:8001/health
# 5. Kiểm tra reflection gRPC — CHỈ hoạt động khi ASPNETCORE_ENVIRONMENT=Development
grpcurl -plaintext localhost:8001 list
grpcurl -plaintext localhost:8001 list shoppingcart.v1.UsersApi
# 6. Chạy full stack bằng Docker (DB nội bộ user-db:5432, publish ra host 5433)
docker compose up -d --build
curl -i http://localhost:8001/health
```
**Kỳ vọng (số liệu từ audit, phiên tài liệu này KHÔNG chạy `dotnet`):**
| Bước | Kỳ vọng |
|:---|:---|
| `dotnet build` | 0 error; 5 project build thành công |
| `dotnet test` | **9** test pass, **0** fail (toàn bộ nằm trong `AuthServiceTests`) |
| `grpcurl ... list` | Trả **13** method của `UsersApi` (reflection đọc từ `shop.proto`), nhưng chỉ **4** method chạy được — 9 method còn lại trả `Unimplemented` |
| `/health` | Trả 200 kể cả khi **DB đã chết** — endpoint này không kiểm tra DB, **không** dùng làm bằng chứng "service hoạt động" |
### 1.1 Kiểm tra hành vi 4 rpc đã implement
```bash
# Register (rpc ✅) — trả về UserProfile với Status = PENDING_VERIFICATION (0)
grpcurl -plaintext -d '{"email":"a@b.com","password":"Password123!","first_name":"A","last_name":"B","username":"ab"}' \
  localhost:8001 shoppingcart.v1.UsersApi/Register
# Login (rpc ❌) — kỳ vọng: Unimplemented, KHÔNG phải token
grpcurl -plaintext -d '{"username":"ab","password":"Password123!"}' \
  localhost:8001 shoppingcart.v1.UsersApi/Login
```
⚠️ Muốn `Register` chạy thành công phải có **PostgreSQL sống** (nếu migrate thất bại, `Program.cs:49-62` chỉ log warning và service vẫn khởi động ⇒ mọi query sẽ ném lỗi kết nối ở runtime). Redis **không bắt buộc** để service khởi động, nhưng khi Redis chết thì OTP không được lưu và `VerifyOtp` luôn trả `OTP_EXPIRED`.
---
## 2. CHECKLIST TRƯỚC KHI COMMIT CHO AI AGENTS
- [ ] **Biên dịch thành công 100%**: `dotnet build User.sln -c Release` → 0 error.
- [ ] **Test xanh**: `dotnet test User.sln -c Release` → 9/9 pass. Nếu sửa `AuthService`, phải cập nhật/bổ sung test tương ứng (file duy nhất: `User.Tests/AuthServiceTests.cs`).
- [ ] **Mật khẩu được băm an toàn**: hiện dùng **BCrypt** (`BCrypt.Net-Next 4.0.3`, `AuthService.cs:75,95`) ✅ — ⚠️ **không** có Argon2id (đặc tả cũ ghi "Argon2/BCrypt" là chưa chính xác: chỉ có BCrypt).
- [ ] **EF Core trực tiếp**: không có class/interface Repository nào bọc `DbContext` (grep `Repository` trên `.cs` = 0) ✅.
- [ ] **Port & Database đúng chuẩn**: gRPC + HTTP cùng port `8001` (`Dockerfile:3-4`, `launchSettings.json`); DB `usermanagement_db` publish ra host port `5433`, trong mạng Docker là `user-db:5432` (`docker-compose.yml`). ❌ **Không** dùng tên `user_db` (đặc tả cũ sai).
- [ ] **Không thêm secret mới**: mật khẩu Postgres/RabbitMQ đang là giá trị mặc định hardcode trong `docker-compose.yml:17,42,46` và `.env.example:8,14` — đây là **phát hiện P0 của audit**, không được nhân bản thêm. Khi cần, đọc từ biến môi trường, không chép giá trị vào tài liệu.
- [ ] **Git Status sạch sẽ**: không commit `bin/`, `obj/`, `.idea/`, `.env`.
- [ ] **Không sửa `Contracts/shop.proto`** khi chưa chốt nguồn sự thật Protobuf toàn hệ thống.
---
## 3. CÁC ĐIỂM PHẢI KIỂM TRA KHI SỬA CODE (GAP ĐÃ BIẾT)
| # | Điểm cần kiểm | Vì sao |
|--:|:---|:---|
| 1 | `UsersGrpcService` chỉ override **4/13** rpc (`:25`, `:72`, `:89`, `:119`) | Thêm/sửa rpc phải thêm override, nếu không sẽ trả `Unimplemented` |
| 2 | `AuthService` publish event **sau** `SaveChangesAsync` (`:139` → `:147`) | Có thể làm message outbox không được persist (xem `03_EVENT_MESSAGING.md` §2.1) |
| 3 | OTP sinh bằng `Random.Shared.Next(100000, 999999)` (`:133`, `:245`) | Không phải CSPRNG; không có rate-limit ⇒ có thể brute-force OTP 6 số |
| 4 | Log chứa **OTP thật** khi bus offline (`:157`) | Rò rỉ bí mật vào log |
| 5 | `HasQueryFilter` chỉ phủ **8/14** entity | 6 bảng còn lại sẽ trả cả bản ghi đã xóa mềm |
| 6 | 10/14 `DbSet` chưa dùng | Schema rộng hơn nghiệp vụ; đừng giả định tính năng đã có (refresh token, seller, report, penalty, audit…) |
| 7 | Không có authentication/authorization trong pipeline | Mọi rpc đều public; `x-user-id` do client tự khai ⇒ **không** tin header này như bằng chứng danh tính |