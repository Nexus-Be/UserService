# [POST] /api/v1/me/email/change-request

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Profile`  
> **Mã tham chiếu Checklist:** `USER-10`  
> **Trạng thái tài liệu:** `B_none (Chờ hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `⏳ IN_PROGRESS`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Yêu cầu đổi email đăng nhập & gọi REST nội bộ sang NotificationService để gửi OTP về email mới.
* **HTTP Method:** `POST`
* **Đường dẫn chuẩn:** `/api/v1/me/email/change-request`
* **Quyền hạn truy cập (RBAC):** `RequireRole (Authenticated)`
* **Yêu cầu Idempotency:** Khuyến nghị gửi `Idempotency-Key`
