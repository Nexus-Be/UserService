# 00 — NHIỆM VỤ CỐT LÕI & RÀO CHẮN CHỐNG ẢO GIÁC (USER SERVICE)

---

## 1. NHIỆM VỤ & RANH GIỚI MIỀN (BOUNDED CONTEXT)

**UserService** chịu trách nhiệm độc quyền về việc quản lý định danh người dùng và kiểm soát truy cập (Identity & Access Management) cho toàn bộ hệ thống Nexus:
- Đăng ký người dùng (`Register`), xác thực đăng nhập (`Login`), phát hành JSON Web Token (JWT).
- Quản lý hồ sơ người dùng (`UserProfile`), cập nhật email, tên hiển thị.
- Quản lý quy trình nâng cấp người bán (`SellerRegistration`), duyệt/từ chối hồ sơ seller bởi nhân viên vận hành (`staff`).
- Quản trị phân quyền theo vai trò (RBAC): Gán role, danh mục roles (`buyer`, `seller`, `staff`, `admin`), danh mục quyền hạn (`permissions`).
- Cung cấp dịch vụ gRPC Server tại cổng `8001` cho ApiGateway và các service khác xác thực người dùng.

---

## 2. NHỮNG ĐIỀU CẤM KỴ ĐỐI VỚI AI AGENTS (ANTI-HALLUCINATION GUARDRAILS)

1. **TUYỆT ĐỐI KHÔNG DÙNG REPOSITORY PATTERN BỌC NGOÀI EF CORE**:
   - Tuân thủ quyết định kiến trúc [ADR.md](../../ApiGateway/ADR.md).
   - Sử dụng trực tiếp `UserDbContext` và `DbSet<T>` trong tầng Application (thông qua interface `IUserDbContext` để trừu tượng hóa cho Unit Testing).
   - Tuyệt đối **KHÔNG** tạo `IUserRepository`, `GenericRepository`, `UnitOfWork`.

2. **TUYỆT ĐỐI KHÔNG LƯU MẬT KHẨU DẠNG PLAIN TEXT**:
   - Mật khẩu người dùng phải được băm (hash) bằng **Argon2id** (hoặc BCrypt) có salt ngẫu nhiên trước khi lưu vào DB.

3. **TUYỆT ĐỐI KHÔNG THAY ĐỔI CỔNG 8001 VÀ DATABASE 5433**:
   - Service gRPC chạy tại `http://localhost:8001`.
   - Chuỗi kết nối DB trỏ tới `localhost:5433/usermanagement_db` với mật khẩu mặc định `MatkhaucuaTui123@`.

4. **TUYỆT ĐỐI KHÔNG TỰ Ý SỬA PROTOBUF MÀ KHÔNG ĐỒNG BỘ GATEWAY**:
   - Mọi RPC method và schema message phải khớp 100% với `Contracts/shop.proto` tại ApiGateway.
