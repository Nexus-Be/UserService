# [GET] /api/v1/reputation/policy

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `ReputationPolicy`  
> **Mã tham chiếu Checklist:** `POLICY-01`  
> **Trạng thái tài liệu:** `B_none (Chờ hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `⏳ IN_PROGRESS`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Cung cấp thông số cấu hình ngưỡng uy tín toàn sàn (`minReputationToBid: 40`, `minReputationToCreateAuction: 50`) cho Frontend hiển thị nhãn và chặn thao tác sớm.
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/reputation/policy`
* **Quyền hạn truy cập (RBAC):** `AllowAnonymous`
* **Yêu cầu Idempotency:** Không áp dụng (Idempotent theo HTTP spec)
