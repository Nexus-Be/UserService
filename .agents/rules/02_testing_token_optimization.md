# QUY CHUẨN KIỂM THỬ TIẾT KIỆM TOKEN & XỬ LÝ SỰ CỐ (TESTING & TROUBLESHOOTING)
> **Mục tiêu:** Tăng tốc chu kỳ phản hồi, cắt giảm 90% lượng token in ra terminal, và ngăn ngừa lỗi lặp lại.

---

## 1. CHIẾN LƯỢC TEST 2 BƯỚC ("BẮN TỈA" VS "TỔNG DUYỆT") 🎯
1. **Trong lúc code (Vòng lặp phát triển):**
   * **TUYỆT ĐỐI KHÔNG CHẠY CẢ SOLUTION `dotnet test User.sln`** vì log dài hàng trăm dòng nuốt sạch token và rất chậm.
   * **Bắt buộc dùng cờ lọc trúng đích:** Chỉ định file test hoặc hàm test của tính năng đang code:
     ```powershell
     dotnet test User.Tests/User.Tests.csproj --filter "FullyQualifiedName~AuthControllerRestTests" --logger "console;verbosity=minimal"
     ```
   * **Tận dụng `--no-build`:** Khi code đã build thành công, thêm `--no-build -c Release` để chạy test tức thì mà không build lại.  
     *(⚠️ **Điều kiện tiên quyết:** Cờ `--no-build -c Release` BẮT BUỘC chỉ chạy sau khi đã chạy `dotnet build User.sln -c Release` thành công trước đó. Nếu vừa sửa mã nguồn C# mà chưa build, bắt buộc phải build trước hoặc bỏ cờ `--no-build` để tránh lỗi thiếu file DLL).*
2. **Trước khi bàn giao / commit (Definition of Done):**
   * Chỉ lúc này mới chạy toàn bộ solution **1 lần duy nhất** để kiểm tra hồi quy:
     ```powershell
     dotnet test User.sln -c Release --logger "console;verbosity=minimal"
     ```

---

## 2. QUY TRÌNH GHI SỔ TAY SỰ CỐ KHI LỖI LẶP LẠI ($\ge 2$ LẦN FAIL) 🚨
* Nếu trong quá trình code hoặc chạy test mà bị **fail $\ge 2$ lần liên tiếp**, hoặc sau khi commit bị lỗi hồi quy:
  1. AI Agent **BẮT BUỘC DỪNG LẠI**, không được spam sửa code hú họa.
  2. Tạo 1 file ghi nhận sự cố mới vào thư mục:
     `D:/obsidian/vinhth/UserService - học/Troubleshooting/YYYY-MM-DD_[ten-loi].md`
  3. Cấu trúc file gồm:
     - Triệu chứng & Stacktrace lỗi.
     - Phân tích nguyên nhân gốc rễ (Root Cause).
     - Cách khắc phục triệt để (Before vs After code).
     - Quy tắc phòng ngừa để không bao giờ tái phạm.
  4. Cập nhật 1 dòng tóm tắt vào bảng tra cứu master:
     `D:/obsidian/vinhth/UserService - học/Troubleshooting/00_SO_TAY_TRA_CUU_LOI_THUONG_GAP.md`
