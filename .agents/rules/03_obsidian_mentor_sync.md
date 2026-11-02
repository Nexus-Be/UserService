# QUY CHUẨN ĐỒNG BỘ OBSIDIAN VAULT & MENTOR WORKFLOW
> **Mục tiêu:** Đồng bộ nhật ký học tập, quản lý tiến độ và review code minh bạch cùng Mentor.  
> **Obsidian Vault:** `D:/obsidian/vinhth/UserService - học/`

---

## 1. NGUYÊN TẮC PHÂN TÁCH KHÔNG GIAN LƯU TRỮ 📂
* **TUYỆT ĐỐI KHÔNG commit các file markdown `.md` ghi chú bừa bãi vào source code dự án.**
* Toàn bộ tài liệu học tập, thực chiến và lý thuyết được lưu tập trung trong Obsidian Vault theo 4 trụ cột:
  1. `Tasks/`: Nhật ký thực chiến từng Task (`YYYY-MM-DD_HHmm_[task].md`).
  2. `Concepts/`: Sổ tay lý thuyết, mẫu thiết kế, nguyên lý (`YYYY-MM-DD_[concept].md` — quy ước tên file chỉ dùng ngày).
  3. `Troubleshooting/`: Sổ tay sự cố, cạm bẫy lỗi thường gặp và cách fix (`YYYY-MM-DD_[error-name].md` và Master Index).
  4. `Flows/`: Bản đồ truy vết luồng hệ thống End-to-End (`YYYY-MM-DD_flow_[ten-luong].md`).

---

## 2. QUY TRÌNH KHI KẾT THÚC TASK (POST-TASK GIT TRACKING) 📝
Mỗi khi hoàn thành một task, AI Agent bắt buộc phải cung cấp:
1. **Tóm tắt code cốt lõi:** Chỉ nêu các đoạn code trọng tâm (trước vs sau), không paste lại cả file.
2. **Lệnh Git chuẩn mực (CẤM `git add .`):**
   ```bash
   git status                                  # Soi chiếu chính xác các file đã sửa
   git add <duong-dan-file-can-commit>        # CHỈ add đúng file nghiệp vụ, cấm git add .
   git commit -m "<type>(<scope>): <subject>"
   git log -1 --oneline
   ```
3. **Mã Commit Hash:** Trích xuất mã commit 7 ký tự (ví dụ: `fb0cc5f`) điền trực tiếp vào file Task Log để Mentor dễ dàng review bằng lệnh:
   ```bash
   git show <commit_hash> --stat
   ```
4. **Chiết xuất Khái niệm mới (Concept Extraction):** Nếu task có phát sinh kiến thức kỹ thuật mới (RESTful, Transactional Outbox, JWT, Redis, Partial Index...), tự động tạo một file mới trong `Concepts/` theo định dạng `YYYY-MM-DD_[ten-khai-niem].md` (kèm mốc thời gian ghi chép chi tiết bên trong nội dung file) để hiển thị trong Graph View của Obsidian.
