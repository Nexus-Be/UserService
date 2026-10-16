# 04 — MÔ HÌNH THỰC THỂ & CƠ SỞ DỮ LIỆU (USER SERVICE DATA PERSISTENCE)

---

## 1. MÔ HÌNH THỰC THỂ CHÍNH (DOMAIN ENTITIES)

1. **`User`**:
   - `Id`: `Guid` (Primary Key).
   - `Username`: `string` (Unique, Max 50 chars).
   - `Email`: `string` (Unique, Max 100 chars).
   - `PasswordHash`: `string` (Argon2 hash chuỗi ký tự).
   - `DisplayName`: `string` (Max 100 chars).
   - `Status`: `string` (`"Active"`, `"Suspended"`, `"PendingVerification"`).
   - `CreatedAt`, `UpdatedAt`: `DateTimeOffset`.
   - Navigation: `ICollection<UserRole> Roles`, `SellerRegistration? SellerRegistration`.

2. **`Role`**:
   - `Id`: `Guid`.
   - `Name`: `string` (Unique: `"buyer"`, `"seller"`, `"staff"`, `"admin"`).
   - `Description`: `string`.

3. **`UserRole`**:
   - `UserId`: `Guid` (FK $\rightarrow$ `User.Id`).
   - `RoleId`: `Guid` (FK $\rightarrow$ `Role.Id`).
   - Khóa chính tổng hợp (Composite PK: `UserId` + `RoleId`).

4. **`SellerRegistration`**:
   - `Id`: `Guid`.
   - `UserId`: `Guid` (Unique FK $\rightarrow$ `User.Id`).
   - `ShopName`: `string` (Max 150 chars).
   - `Description`: `string`.
   - `ContactEmail`, `ContactPhone`, `Address`: `string`.
   - `Status`: `string` (`"Pending"`, `"Approved"`, `"Rejected"`).
   - `RejectionReason`: `string?`.
   - `ReviewedByStaffId`: `Guid?`.
   - `ReviewedAt`: `DateTimeOffset?`.

---

## 2. QUY CHUẨN EF CORE & MIGRATIONS

- Sử dụng `Npgsql.EntityFrameworkCore.PostgreSQL`.
- Kết nối: `Host=localhost;Port=5432;Database=usermanagement_db;Username=postgres;Password=...`.
- Tên bảng theo dạng snake_case (`users`, `roles`, `user_roles`, `seller_registrations`).
- Khởi tạo Migration:
  ```bash
  dotnet ef migrations add InitialCreate --project User.Infrastructure --startup-project User.Api
  dotnet ef database update --project User.Infrastructure --startup-project User.Api
  ```
- **Tuyệt đối KHÔNG dùng Repository Pattern bọc ngoài**: Triển khai nghiệp vụ gọi trực tiếp `_context.Users.AddAsync()`, `_context.Users.FirstOrDefaultAsync()`, `_context.SaveChangesAsync()`.
