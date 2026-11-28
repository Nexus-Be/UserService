---
name: service-api-doc
description: Pre-code API documentation standard for UserService (port 8001). Mandates drafting a service-level technical API doc before coding, referencing existing domain entities, enums, DTOs, and error patterns for system-wide consistency. Governs pure DTO contracts, validation attributes, and RFC 9110 error matrices inside UserService.
---

# UserService API Pre-Code Documentation Skill

> **Scope:** UserService repository only (`src/Services/User/`). 100% Inward-Only. Never reference the root workspace or sibling services.

This skill governs the **mandatory pre-code documentation lifecycle** within UserService. Before any C# code is written or modified for a REST endpoint, the developer or AI agent must create a technical API specification inside `docs/api/`.

---

## 1. Ranh giới Tài liệu UserService

* **Vị trí lưu trữ:**
  - API đã hoàn tất & kiểm chứng: `docs/api/A_Active/User/{Feature}/{HTTP_METHOD}_{ENDPOINT_SLUG}.md`
  - API trong quá trình đặc tả/chờ làm: `docs/api/B_none/User/{HTTP_METHOD}_{ENDPOINT_SLUG}.md`
* **File mẫu chuẩn:** `docs/api/API_DOC_TEMPLATE.md`
* **Quy chuẩn Tác giả mặc định:** `> **Tác giả:** Hiển Vinh & AI Agent`
* **Thời điểm lập:** **BẮT BUỘC TRƯỚC KHI VIẾT CODE (Pre-Code)**.
* **Định dạng dữ liệu trả về:** **Pure DTO** hoặc `PagedData<T>` (tuyệt đối không bọc qua `ApiResponse<T>`).
* **Rào chắn Inward-Only:** Chỉ trỏ tới tài nguyên nội bộ của UserService (`User.Domain`, `User.Application`, `User.Infrastructure`, `User.Api`). Không tham chiếu tới `docs/api/` ở Root.

---

## 2. Quy trình 3 Bước Tra cứu Source of Truth Trước Khi Lập Doc

### Bước 1: Tra cứu Thực thể Domain (`User.Domain/Entities/`)
- Mở `User.Domain/Entities/` (ví dụ `User.cs`, `Role.cs`, `Permission.cs`, `SellerRegistration.cs`, `UserSession.cs`).
- Xác định kiểu C#, thuộc tính nullable, và các ràng buộc trường dữ liệu.

### Bước 2: Tra cứu Bảng mã Enum (`User.Domain/Enums/`)
- Mở `User.Domain/Enums/` (ví dụ `UserStatus.cs`, `SellerRegistrationStatus.cs`).
- Ghi nhận số nguyên DB (`0`, `1`, `2`...) và chuỗi PascalCase API (`Active`, `PendingVerification`, `Banned`).
- Điền bảng ánh xạ vào Mục 4.1 của doc.

### Bước 3: Tra cứu Mẫu DTO & Xử lý Lỗi Có Sẵn (`User.Application/`)
- Rà soát các tính năng có sẵn tại `User.Application/Services/` (ví dụ `Auth/`, `Users/`, `Sellers/`).
- Kế thừa quy ước đặt tên DTO: `{Action}Request`, `{Action}Response`.
- Kế thừa mã lỗi đã có (`EMAIL_ALREADY_EXISTS`, `USER_NOT_FOUND`, `INVALID_CREDENTIALS`, `VAL_INVALID_INPUT`).

---

## 3. Các Rào Chắn Bất Biến tại UserService

1. **Pure DTO:** Controller tại UserService trả về DTO thuần, cấm thêm `ResultFilter` hay bọc `ApiResponse<T>`.
2. **Plural Route:** `/api/v1/users`, `/api/v1/admin/users`, `/api/v1/sellers`. Ngoại lệ duy nhất: `/api/v1/me` và `/api/v1/auth`.
3. **Mã lỗi & Message:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (cấm tiền tố `ERR_`).
4. **Bảo mật danh tính & Ownership:** Tuyệt đối không tin tưởng `userId` từ client. Luôn đọc từ `HttpContext.GetUserId()`.
5. **Phân trang tại DB:** Sử dụng `PageQuery` và trả về `PagedData<T>` được tính toán qua `Skip`/`Take` và `CountAsync`.

---

## 4. Quy trình Thực hiện (Workflow)

1. Sao chép `docs/api/SERVICE_API_DOC_TEMPLATE.md` vào `docs/api/{Feature}/{HTTP_METHOD}_{ENDPOINT_SLUG}.md`.
2. Điền đầy đủ 5 phần của template dựa trên kết quả tra cứu Source of Truth.
3. Hoàn thiện mục 5.1 (Pre-Code Plan) mô tả các bước xử lý kỹ thuật.
4. Triển khai code C# tuân thủ 100% hợp đồng đã đặc tả trong doc.
5. Chạy `dotnet test User.sln -c Release` đạt 100% pass, tích checklist và chuyển trạng thái doc sang `✅ VERIFIED`.
