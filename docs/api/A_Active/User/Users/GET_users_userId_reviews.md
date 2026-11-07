# [GET] /api/v1/users/{userId}/reviews

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Review`  
> **Mã tham chiếu Excel:** `24`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `✅ VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Xem danh sách đánh giá công khai của một người dùng hoặc gian hàng (đã tự động lọc bỏ các đánh giá bị ẩn `IsDisabled == true` và che tên người đánh giá `ReviewerDisplayNameMasked`).
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/users/{userId}/reviews`
* **Quyền hạn truy cập (RBAC):** `AllowAnonymous` (Công khai)
* **Yêu cầu Idempotency:** Không áp dụng (Idempotent theo HTTP spec)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/UsersController.cs` $\rightarrow$ `GetUserReviews()`
* **Application Interface:** `User.Application/Services/Review/IReviewService.cs` $\rightarrow$ `GetUserReviewsAsync()`
* **Service Implementation:** `User.Application/Services/Review/ReviewService.cs` $\rightarrow$ `GetUserReviewsAsync()`
* **Item DTO:** `User.Application/Services/Review/ReviewDtos.cs` $\rightarrow$ `UserReviewItemDto`
* **Thực thể & Bảng DB:** `User.Domain/Entities/UserReview.cs` $\rightarrow$ Bảng `UserReviews` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters
| Tên tham số | Kiểu dữ liệu | Bắt buộc? | Ràng buộc & Định dạng |
|:---|:---|:---:|:---|
| `userId` | `Guid (UUID)` | Có | Mã định danh của người nhận đánh giá (`RevieweeId`) |

### 3.2. Query Parameters
| Tên tham số | Kiểu dữ liệu | Mặc định | Ràng buộc & Khoảng giá trị |
|:---|:---|:---:|:---|
| `page` | `integer` | `1` | Số trang (tối thiểu 1) |
| `pageSize` | `integer` | `20` | Kích thước trang ($1 \le pageSize \le 100$) |
| `rating` / `filterRating` | `integer?` | `null` | Lọc theo số sao (`1..5`) |
| `reviewerRole` | `ReviewerRole?` | `null` | Lọc theo vai trò người đánh giá (`Buyer` hoặc `Seller`) |

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "items": [
    {
      "reviewId": "40000000-aaaa-bbbb-cccc-000000000001",
      "reviewerId": "dddddddd-4444-4444-4444-444444444444",
      "reviewerDisplayNameMasked": "L***g",
      "reviewerAvatarUrl": null,
      "reviewerRole": "Buyer",
      "rating": 5,
      "comment": "Fast delivery and authentic item.",
      "mediaUrls": [
        "https://images.nexus.local/reviews/user-review-01.png"
      ],
      "referenceType": "Order",
      "referenceId": "02000000-0000-0000-0000-000000000001",
      "createdAt": "2026-11-07T03:00:00Z"
    }
  ],
  "pagination": {
    "currentPage": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1,
    "hasPrevious": false,
    "hasNext": false
  }
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `404` | `USER_NOT_FOUND` | "User account was not found for the specified identifier." | Không tìm thấy người dùng theo `userId`. |
| `500` | `INTERNAL_SERVER_ERROR` | "An unexpected error occurred. Please try again later." | Lỗi hệ thống nội bộ chưa được xử lý. |

---

## 6. DANH MỤC ENUM LIÊN QUAN (DOMAIN ENUMS)

| Tên trường Payload (JSON Field) | Kiểu C# Enum | Giá trị DB (`integer`) | Giá trị API (`string PascalCase`) | Ý nghĩa nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `reviewerRole` | `ReviewerRole` | `0`, `1` | `Buyer`, `Seller` | Vai trò của người viết đánh giá trong giao dịch. |
| `referenceType` | `ReferenceType` | `0`, `1` | `Order`, `Auction` | Loại giao dịch gốc. |

---

## 7. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IReviewService.cs` và `ReviewService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `UsersController.cs` (`GET /api/v1/users/{userId}/reviews`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test` vượt qua 100% (`ReviewServiceTests.cs`).

