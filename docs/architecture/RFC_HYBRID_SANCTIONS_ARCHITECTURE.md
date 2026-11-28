# TÀI LIỆU ĐỀ XUẤT KIẾN TRÚC (RFC / ADR)
## Xử Lý Vi Phạm Đa Vai Trò (Contextual Sanctions) Cho User Service
* **Mã đề xuất:** RFC-USER-2026-001
* **Vị trí áp dụng:** UserService (Port 8001, DB `usermanagement_db:5433`)
* **Tác giả:** Senior Backend Architect
* **Trạng thái:** Chờ Lead phê duyệt (Pending Approval)
* **Phương án lựa chọn:** Phương án 3 — Hybrid State Projection Pattern

---

## 1. BỐI CẢNH & VẤN ĐỀ CẦN GIẢI QUYẾT

Hệ thống sàn TMĐT Nexus là mô hình đa người bán (Multi-vendor Marketplace). Một tài khoản (`Users`) có thể đồng thời đóng hai vai trò:
* **Người mua (Buyer)**: Năng lực mặc định của mọi tài khoản hợp lệ (`Status == UserStatus.Active`).
* **Người bán (Seller)**: Năng lực gắn liền với hồ sơ gian hàng (`SellerProfiles`).

### Bất cập hiện tại trong mã nguồn:
1. **Không xử phạt được theo vai trò (Contextual Sanction)**:
   * Hiện tại `AdminUserService.BanUserAsync` chỉ có một cơ chế duy nhất: **Khóa cứng toàn bộ tài khoản (`UserStatus.Banned`)**.
   * Nếu Seller vi phạm bán hàng (giao trễ, hàng lỗi) $\rightarrow$ Khóa luôn tài khoản, họ không thể đi mua sắm.
   * Nếu Buyer vi phạm (bom hàng COD, lạm dụng hoàn tiền) $\rightarrow$ Cần hạn chế mua hàng nhưng hiện tại nếu ban thì sập luôn Shop đang bán tốt của họ.
2. **Sai lệch trạng thái Domain Model ("Nói dối" trong DB)**:
   * Enum `SellerProfileStatus` hiện chỉ có: `Pending = 0`, `Approved = 1`, `Rejected = 2`.
   * Khi một Shop bị cấm kinh doanh, trường `Status` của Shop trong DB vẫn ghi là `Approved` vì không có trạng thái đình chỉ (`Suspended`).
3. **Dead Schema trong Database**:
   * Bảng `UserPenalties` đã được tạo trong PostgreSQL nhưng **chưa có dòng code Application nào đọc/ghi** (0 điểm đọc/ghi).
   * Mọi án phạt không có thời hạn (`ExpiresAt`), không có lý do điều khoản, không có bằng chứng hay cơ chế khiếu nại (Appeals).

---

## 2. PHƯƠNG ÁN ĐỀ XUẤT: HYBRID STATE PROJECTION PATTERN

Tách biệt rõ ràng 2 trách nhiệm giữa **Sổ cái Lịch sử (Audit Ledger)** và **Trạng thái Vận hành Tức thời (Operational State)**:

```
                  [ Admin / Compliance Staff Ban Hành Phạt ]
                                      │
                                      ▼
               ┌──────────────────────────────────────────────┐
               │         1 Transaction Duy Nhất (PostgreSQL)  │
               ├──────────────────────────────────────────────┤
               │ 1. Ghi Bảng USER_PENALTIES (Sổ cái lịch sử)  │
               │    - TargetRole: Buyer, Seller, Account      │
               │    - PenaltyApplied: Warning, Restrict, Ban  │
               │    - ExpiresAt, PolicyClauseCode, Evidence   │
               │                                              │
               │ 2. Cập nhật State Projection (Vận hành)      │
               │    - Nếu phạt Account: USERS.Status = Banned │
               │    - Nếu phạt Seller:  SELLER.Status = Suspend│
               └──────────────────────┬───────────────────────┘
                                      │
                                      ▼
               ┌──────────────────────────────────────────────┐
               │        Cập nhật / Invalidate Redis Cache     │
               │        (Key: user:sanctions:{userId})        │
               │        TTL = ExpiresAt - DateTimeOffset.UtcNow│
               └──────────────────────────────────────────────┘
```

### Cách thức kiểm tra quyền khi User thao tác (Tần suất triệu request/phút):
* **Đăng nhập**: Đọc trực tiếp `user.Status == UserStatus.Active` $\rightarrow$ Tốc độ $O(1)$.
* **Bán hàng / Đăng sản phẩm**: Đọc trực tiếp `seller.Status == SellerProfileStatus.Approved` $\rightarrow$ Tốc độ $O(1)$ (Shop bị `Suspended` bị chặn ngay).
* **Mua hàng / Checkout**: API Gateway hoặc Order Service kiểm tra nhanh Redis Cache (`user:sanctions:{userId}`) $\rightarrow$ Thời gian $< 1\text{ms}$.

---

## 3. CHI TIẾT THAY ĐỔI ENUMS & DATABASE SCHEMA

### 3.1. Danh sách Enum cần thêm & sửa

#### 1. Thêm mới Enum `TargetRole`
* **File:** `User.Domain/Enums/TargetRole.cs`
```csharp
namespace User.Domain.Enums;

public enum TargetRole
{
    Buyer = 0,    // Chỉ chế tài vai trò Người mua (chặn COD, cấm đặt đơn)
    Seller = 1,   // Chỉ chế tài vai trò Người bán (đình chỉ gian hàng)
    Account = 2   // Chế tài toàn bộ tài khoản (khóa đăng nhập toàn sàn)
}
```
* **Lý do:** Xác định rõ đối tượng chịu hình phạt. Tách biệt hoàn toàn với bảng `Roles` hệ thống (Admin, SupportStaff).

#### 2. Bổ sung Enum `SellerProfileStatus` (Thêm giá trị `3`)
* **File:** `User.Domain/Enums/SellerProfileStatus.cs`
```csharp
namespace User.Domain.Enums;

public enum SellerProfileStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Suspended = 3 // <-- BỔ SUNG MỚI
}
```
* **Lý do & Độ an toàn:**
  * Giúp trạng thái Shop phản ánh đúng thực tế kinh doanh.
  * Trong code hiện tại, mọi logic kiểm tra Shop hợp lệ đều dùng `sp.Status == SellerProfileStatus.Approved`. Khi gán `Suspended`, Shop lập tức bị vô hiệu hóa an toàn mà **không cần sửa lại hàng chục câu query LINQ cũ**.
  * Lưu dạng `integer` trong PostgreSQL, thêm vào cuối enum nên **100% Backward Compatible (Zero Breaking Change)**.

#### 3. Chuẩn hóa Enum `PenaltyType`
* **File:** `User.Domain/Enums/PenaltyType.cs`
```csharp
namespace User.Domain.Enums;

public enum PenaltyType
{
    Warning = 0,
    DeductPoints = 1,
    Restriction = 2, // Thay thế SuspendBidding3Days cũ
    Ban = 3,
    Other = 4
}
```
* **Lý do:** Enum cũ `SuspendBidding3Days` bị fix cứng số ngày "3Days". Thay bằng `Restriction` kết hợp với cột ngày hết hạn `ExpiresAt` giúp linh hoạt cho mọi thời hạn (1 ngày, 3 ngày, 30 ngày).

---

### 3.2. Thay đổi Database: Mở rộng bảng `UserPenalties`

> [!NOTE]
> **KHÔNG TẠO THÊM BẢNG MỚI.** Tận dụng bảng `UserPenalties` đã có sẵn trong migration `InitialCreate`.

* **File:** `User.Domain/Entities/UserPenalty.cs` & `UserPenaltyConfiguration.cs`

| Tên cột mới | Kiểu dữ liệu PostgreSQL | Nullable | Ý nghĩa nghiệp vụ |
| :--- | :--- | :---: | :--- |
| **`TargetRole`** | `integer` | ❌ NOT NULL (Default: `2`) | Vai trò bị phạt: `Buyer=0`, `Seller=1`, `Account=2`. |
| **`ReportId`** | `uuid` | ✅ NULL | Khóa ngoại nối tới `UserReports` nếu phạt từ đơn tố cáo. |
| **`PolicyClauseCode`** | `varchar(100)` | ✅ NULL | Mã điều khoản vi phạm (vd: `TOS_SELLER_4_1`, `BUYER_COD_ABUSE`). |
| **`EvidenceUrls`** | `text[]` | ✅ NULL | Mảng URL ảnh/tài liệu bằng chứng vi phạm. |
| **`RevokedBy`** | `uuid` | ✅ NULL | Admin ID người duyệt gỡ phạt khi khiếu nại thành công. |
| **`RevokedAt`** | `timestamptz` | ✅ NULL | Thời điểm gỡ phạt. |
| **`RevokeReason`** | `varchar(1000)` | ✅ NULL | Lý do chấp thuận gỡ phạt. |

---

### 3.3. Tối ưu Hiệu năng với PostgreSQL Partial Index
* **File:** `User.Infrastructure/Persistence/Configurations/UserPenaltyConfiguration.cs`
```csharp
builder.HasIndex(p => new { p.UserId, p.TargetRole, p.PenaltyApplied, p.ExpiresAt })
       .HasDatabaseName("IX_UserPenalties_Active_Evaluation")
       .HasFilter("\"Status\" = 0"); // 0 = PenaltyStatus.Active
```
* **Giải thích:** Index này chỉ gom các án phạt **đang Active**, bỏ qua 95% các bản ghi đã `Expired` hoặc `Revoked`. Câu query kiểm tra quyền chỉ quét tập index nhỏ vài chục KB, thời gian truy vấn $< 1\text{ms}$.

---

## 4. CHIẾN LƯỢC BỘ NHỚ REDIS (CACHE LIFECYCLE)

Để không gây Memory Leak trên Shared Redis (500MB):
1. **Chỉ lưu Blacklist (Negative Caching)**:
   * 99.9% người dùng bình thường **không có key trên Redis**.
   * Chỉ tạo key `user:sanctions:{userId}` cho người đang chịu án phạt Active.
2. **TTL tự hủy theo án phạt**:
   * Án phạt có thời hạn: $\text{TTL} = \text{ExpiresAt} - \text{DateTimeOffset.UtcNow}$. Hết hạn phạt, Redis tự hủy key, quyền tự động mở lại.
   * Án phạt vĩnh viễn: Đặt TTL trượt 24h (hết hạn tự dọn, chỉ nạp lại nếu kẻ gian cố đăng nhập).
3. **Thu hồi tức thì (Eviction)**: Khi Admin duyệt gỡ phạt, backend gọi `_redis.KeyDeleteAsync(...)` ngay lập tức.

---

## 5. LỘ TRÌNH TRIỂN KHAI (4 PHASES)

* **Phase 1: Domain & Migration**:
  * Tạo enum `TargetRole.cs`, sửa `SellerProfileStatus.cs` (thêm `Suspended`), sửa `PenaltyType.cs`.
  * Cập nhật `UserPenalty.cs` và `UserPenaltyConfiguration.cs`.
  * Chạy `dotnet ef migrations add AddTargetRoleAndSellerSuspendedStatus`.
* **Phase 2: Application Layer & Logic**:
  * Đóng gói logic vào Entity: `user.ApplyAccountBan(...)`, `seller.Suspend(...)`.
  * Refactor `AdminUserService.BanUserAsync` (tự động đình chỉ Shop nếu user là seller).
  * Bổ sung API: Đình chỉ Shop (`AdminSellersController`), Gỡ phạt (`AdminPenaltiesController`).
* **Phase 3: Redis & Fast-Path Endpoint**:
  * Tích hợp ghi cờ phạt vào Redis khi ban hành án phạt.
  * Cung cấp endpoint: `GET /api/v1/users/{id}/permission-check?action=Buy|Sell` phục vụ Gateway & Order Service.
* **Phase 4: Expiration Worker**:
  * Tạo .NET `BackgroundService` định kỳ 5 phút quét các án phạt hết hạn để chuyển `Status = Expired` và khôi phục `SellerProfile.Status = Approved`.

---

## 6. CÂU HỎI MỞ XIN Ý KIẾN TECH LEAD

1. **Về phạm vi phạt Buyer**: Khi Buyer bị phạt vì bom hàng, sàn chặn hoàn toàn đặt đơn mới hay chỉ chặn phương thức COD (bắt thanh toán trước qua thẻ/ví)?
   * *Đề xuất:* Giai đoạn đầu chỉ chặn phương thức COD.
2. **Về thao tác của Seller bị đình chỉ**: Khi Shop bị `Suspended`, có cho phép seller đăng nhập Seller Portal để xem đơn cũ và đối soát rút tiền không?
   * *Đề xuất:* Vẫn cho vào Portal xem đơn cũ, nhưng khóa nút "Tạo sản phẩm" và API Gateway chặn khách mua đặt đơn mới vào shop này.
