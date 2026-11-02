# CẨM NANG ĐIỀU HƯỚNG TỰ ĐỘNG: USER SERVICE LEAD KNOWLEDGE BASE
> **Phạm vi áp dụng:** Local Workspace của Developer (Được exclude khỏi Git).  
> **Nguồn đặc tả nghiệp vụ bổ trợ:** `C:\Users\ACER\Desktop\obsidian\vinhth\User Service Lead`  
> **Quy tắc thứ bậc ưu tiên:** Thư mục `specs/` trong repo `UserService` là **Nguồn sự thật tối cao (`specs/` thắng)**. Kho `User Service Lead` đóng vai trò bổ trợ nghiệp vụ chi tiết. Khi có mâu thuẫn giữa hai bên, **AI BẮT BUỘC PHẢI BÁO CÁO**, tuyệt đối không tự ý suy đoán.  
> **Mục tiêu:** Tự động truy vết và nạp ngữ cảnh nghiệp vụ, CSDL, Enum, API và Business Rules từ kho tài liệu của Lead mà không cần User phải nhắc tên thư mục hoặc tag `@`.

---

## 🎯 1. NGUYÊN TẮC HÀNH XỬ BẮT BUỘC (MANDATORY AGENT BEHAVIOR)
1. **Không suy đoán / Không ảo giác:** Mọi thiết kế Entity, Migration, DTO, Request/Response, Error Code, Endpoint, Enum hay Logic nghiệp vụ BẮT BUỘC phải đối chiếu với tài liệu trong `C:\Users\ACER\Desktop\obsidian\vinhth\User Service Lead` và `specs/`.
2. **Tự động đọc ngầm (Silent Read):** Khi User hỏi hoặc giao task liên quan đến bất kỳ chủ đề nào dưới đây, AI phải chủ động dùng tool `view_file` hoặc `grep_search` trong thư mục `C:\Users\ACER\Desktop\obsidian\vinhth\User Service Lead` trước khi lập Plan hoặc viết code.
3. **Chỉ được phép trỏ đọc — Tuyệt đối cấm chỉnh sửa (Strict Read-Only):** Toàn bộ các thư mục bên ngoài (`Gateway`, `root`, `obsidian`, `Nexus`) và thư mục `specs/` nội bộ đều là tài nguyên **CHỈ ĐƯỢC PHÉP ĐỌC**. Tuyệt đối CẤM can thiệp sửa đổi các tài nguyên này. Mọi ghi chú thảo luận phải được tách riêng ra file Note bên ngoài.

---

## 🧭 2. MA TRẬN TRA CỨU THEO MÃ CHỨC NĂNG (FUNCTION CODES)
Khi User nhắc đến các mã chức năng chuẩn hóa (ví dụ: `AUTH-04`, `USER-01`, `SELLER-01`), AI tự động tra cứu trực tiếp trong:
- 📄 `01. Dac Ta Nghiep Vu & User Stories/06. Bang-Ma-Tran-Chuc-Nang-Command-Query-F5-F6.md`
- 📄 `DAC_TA_TOAN_DIEN_USER_SERVICE_DB_ENUM_FUNCTION.md` (Master Spec All-In-One)

### Bảng phân loại mã chức năng:
| Dải mã | Phân hệ nghiệp vụ | Nội dung cốt lõi |
| :--- | :--- | :--- |
| **`AUTH-01` .. `AUTH-09`** | Xác thực & Phiên làm việc | Đăng ký, OTP, Đăng nhập (JWT/RTR), Quên/Đổi mật khẩu, Logout |
| **`USER-01` .. `USER-11`** | Hồ sơ người dùng & Bảo mật | Xem/Sửa profile, Quản lý Sessions, Avatar, Điểm uy tín, Đổi email |
| **`SELLER-01` .. `SELLER-09`** | Gian hàng & Người bán | Đăng ký mở Shop, Xác thực kho GHN, CCCD, Hồ sơ gian hàng |
| **`ADMIN-01` .. `ADMIN-19`** | Quản trị viên & Kiểm duyệt | Phê duyệt Seller, Khóa/Mở tài khoản, Xử lý vi phạm, Trừ điểm |
| **`REVIEW-01` .. `REVIEW-05`** | Đánh giá đối tác 2 chiều | Review Buyer $\leftrightarrow$ Seller cho cả Đơn hàng & Đấu giá |
| **`SYS-01` .. `SYS-03`** | API RESTful Nội bộ (Inter-Service) | RESTful API cho Gateway, Auction Service & Fulfillment Service (`/api/v1/internal/*`) |
| **`FILE-01` .. `FILE-02`** | Xử lý tệp tin & Media | Upload ảnh avatar cá nhân và tài liệu định danh |

---

## 📚 3. MA TRẬN ĐIỀU HƯỚNG TÀI LIỆU THEO CHỦ ĐỀ KỸ THUẬT

### 🗄️ A. Cơ sở dữ liệu, Entity, Bảng, Cột, ERD, Migration
- **Bản đặc tả CSDL (14 Entity nghiệp vụ, 17 bảng DB thật):** `02. Thiet Ke CSDL & ERD/02. Dac-Ta-11-Bang-CSDL-Nexus.md`  
  *(Lưu ý: Tên file là `Dac-Ta-11-Bang` theo bản nháp ban đầu, nhưng hiện trạng CSDL PostgreSQL `user_db` gồm **14 Entity/DbSet nghiệp vụ** + **3 bảng MassTransit** = **17 bảng vật lý**).*
- **Sơ đồ quan hệ ERD Mermaid:** `02. Thiet Ke CSDL & ERD/01. Thiet-Ke-CSDL-ERD-Chi-Tiet.md`
- **DDL SQL PostgreSQL 16+ nguyên bản:** `05. Seed Data & Scripts/user_service_schema.sql`

### 🏷️ B. Danh mục Enum, Trạng thái (Status, Role, Tier...)
- **15 Enums chuẩn hóa toàn hệ thống:** `01. Dac Ta Nghiep Vu & User Stories/05. Danh-Muc-Enum-Status-Chuan-Hoa.md`
*(Bao gồm: `UserStatus`, `Role`, `TrustLevel`, `SellerRegistrationStatus`, `ViolationType`, `ReviewType`... Cấm tự ý đặt Enum lạ ngoài danh mục này).*

### 🌐 C. Hợp đồng REST API, Endpoint URI, DTO, Request/Response
- **Chi tiết danh mục API Endpoints:** `03. Hop Dong API & Tich Hop/01. Danh-Muc-API-Endpoints-Va-gRPC.md`  
  *(⚠️ **BỎ QUA phần gRPC trong file này.** Toàn bộ API phục vụ giao tiếp đồng bộ bắt buộc dùng RESTful HTTP/JSON theo Rule 4.4).*
- **Bảng đối soát 40 API chuẩn Team Lead:** `03. Hop Dong API & Tich Hop/03. Bang-Hop-Dong-API-Contract-Chuan-Hoa.md`

### 🛡️ D. Phân quyền Hệ thống (Role-Based Authorization - ĐÃ BÃI BỎ RBAC)
- ⚠️ **LƯU Ý QUAN TRỌNG:** Toàn bộ hệ thống **ĐÃ BỎ RBAC CHI TIẾT** (bãi bỏ ma trận 65 Permissions, bảng `Permissions`/`RolePermissions` trong schema không dùng, bãi bỏ rào chắn SoD).
- **Cơ chế phân quyền chuẩn hiện tại:** Phân quyền đơn giản theo 4 Vai trò cốt lõi (`RoleType`: `Buyer = 0`, `Seller = 1`, `SupportStaff = 2`, `Admin = 3`), kiểm tra trực tiếp bằng `[RequireRole("admin", "staff")]` và tiện ích `HttpContextExtensions.GetUserRoles()`.
- **Tài liệu cũ của Lead cần bỏ qua:** Bỏ qua file `05. Seed Data & Scripts/01. Ma-Tran-Phan-Quyen-Va-Huong-Dan-Codebase.md` và `permissions_seed.sql`.

### ⭐ E. Hệ thống Điểm Uy Tín (Reputation) & Đánh Giá Đối Tác
- **Quy tắc cộng/trừ điểm & Rào chắn (Bid $\ge 40$, Sell $\ge 50$):** `01. Dac Ta Nghiep Vu & User Stories/03. He-Thong-Diem-Uy-Tin-Va-Quy-Dinh-San.md`
- **Cơ chế đánh giá 2 chiều Peer Review:** `01. Dac Ta Nghiep Vu & User Stories/04. He-Thong-Danh-Gia-Doi-Tac-Review.md`

### 🔄 F. Kiến trúc Clean Architecture, Outbox Pattern & RabbitMQ
- **Cẩm nang Clean Architecture 5 tầng:** `04. Kien Truc Codebase & Ban Giao/01. Cam-Nang-Kien-Truc-Clean-Architecture.md`
- **Cơ chế Outbox RabbitMQ liên Service:** `03. Hop Dong API & Tich Hop/02. Co-Che-Ket-Noi-Lien-Service-DB-Rieng.md`
- **Bản đồ điều hướng tổng thể:** `00. README - Master Index.md`

### 📱 G. Tài Liệu Flow Tổng Thể Toàn Sàn (BE & FE - Scope Filtering)
Khi tra cứu luồng nghiệp vụ tổng quan từ thư mục `C:\Users\ACER\Desktop\obsidian\`:
- **Backend Flow:** **CHỈ ĐỌC** `BE/BE_01_USER_SERVICE.md` (Tuyệt đối bỏ qua `BE_02` đến `BE_07` của các service khác).
- **Frontend Flow:** **CHỈ ĐỌC** `FE/02_AUTH_USER_VA_SHOP.md` (Bỏ qua các màn hình Đơn hàng, Đấu giá, Ví tiền).
- **Ghi chú tiến độ (Take-note):** Đánh dấu `[x]` kèm mã commit hash trực tiếp vào các mục đã hoàn thành để không đọc lại.
