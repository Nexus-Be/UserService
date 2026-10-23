# 05 — DANH SÁCH KIỂM CHỨNG & NGHIỆM THU (USER SERVICE CHECKLIST)

---

## 1. LỆNH BIÊN DỊCH & CHẠY THỬ NGHIỆM

```bash
# 1. Biên dịch toàn bộ solution
dotnet build User.sln -c Release

# 2. Chạy Service cục bộ (lắng nghe gRPC port 8001)
dotnet run --project User.Api

# 3. Kiểm tra kết nối từ ApiGateway:
# Mở gateway.runtime.json, đổi "users": { "Mode": "Real", "Address": "http://localhost:8001" }
# Gọi POST /gateway/api/v1/auth/login trên Swagger Gateway xem gRPC forward thành công.
```

---

## 2. CHECKLIST TRƯỚC KHI COMMIT CHO AI AGENTS

- [ ] **Biên dịch thành công 100%**: Không có lỗi cú pháp hoặc cảnh báo nghiêm trọng.
- [ ] **Mật khẩu được băm an toàn**: Sử dụng Argon2 hoặc BCrypt.
- [ ] **EF Core trực tiếp**: Không chứa class hoặc interface Repository pattern nào bọc ngoài `DbContext`.
- [ ] **Port & Database đúng chuẩn**: Lắng nghe đúng port `8001`, database `usermanagement_db` trên port `5433` (Password: `MatkhaucuaTui123@`).
- [ ] **Git Status sạch sẽ**: Không có thư mục `bin/`, `obj/`, `.idea/`, file `.env` bị commit vào Git.
