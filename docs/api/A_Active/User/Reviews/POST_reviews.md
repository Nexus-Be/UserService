# [POST] /api/v1/reviews

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Review`  
> **Mã tham chiếu Excel:** `26`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `✅ VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Đánh giá đối tác 2 chiều (`Buyer` $\leftrightarrow$ `Seller`) sau giao dịch và tự động cập nhật đúng quỹ điểm uy tín (`SellerScore` nếu `ReviewerRole == Buyer`, hoặc `CurrentScore` nếu `ReviewerRole == Seller`), kèm cơ chế chống cày điểm **7-Day Anti-Farming Cooldown** cho cùng một cặp giao dịch.
* **HTTP Method:** `POST`
* **Đường dẫn chuẩn:** `/api/v1/reviews`
* **Quyền hạn truy cập (RBAC):** `Authenticated` (`[RequireRole]`)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/ReviewsController.cs` $\rightarrow$ `CreateReview()`
* **Application Interface:** `User.Application/Services/Review/IReviewService.cs` $\rightarrow$ `CreateReviewAsync()`
* **Service Implementation:** `User.Application/Services/Review/ReviewService.cs` $\rightarrow$ `CreateReviewAsync()`
* **Request / Response DTOs:** `User.Application/Services/Review/ReviewRequests.cs` (`CreateReviewRequest`), `ReviewResponses.cs` (`CreateReviewResponse`)
* **Thực thể & Bảng DB:** `User.Domain/Entities/UserReview.cs`, `ReputationScore.cs`, `ReputationLog.cs`

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Request Body (JSON Payload)
```json
{
  "revieweeId": "cccccccc-3333-3333-3333-333333333333",
  "reviewerRole": "Buyer",
  "referenceType": "Order",
  "referenceId": "ORD-2026-001",
  "rating": 5,
  "comment": "Fast delivery and well-packaged product.",
  "mediaUrls": [
    "https://images.nexus.local/reviews/review1.png"
  ]
}
```

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
201 Created:
{
  "reviewId": "40000000-aaaa-bbbb-cccc-000000000001",
  "reviewerId": "dddddddd-4444-4444-4444-444444444444",
  "revieweeId": "cccccccc-3333-3333-3333-333333333333",
  "reviewerRole": "Buyer",
  "targetRole": "Seller",
  "referenceType": "Order",
  "referenceId": "ORD-2026-001",
  "rating": 5,
  "comment": "Fast delivery and well-packaged product.",
  "mediaUrls": [
    "https://images.nexus.local/reviews/review1.png"
  ],
  "scoreImpact": 1,
  "revieweeNewScore": 51,
  "revieweeNewTrustLevel": "Standard",
  "createdAt": "2026-11-07T03:00:00Z"
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `400` | `VAL_INVALID_INPUT` | "Rating must be between 1 and 5 stars." | Số sao ngoài khoảng `1..5` hoặc thiếu `referenceId`. |
| `400` | `SELF_REVIEW_NOT_ALLOWED` | "You cannot submit a review for your own account." | `reviewerId == revieweeId`. |
| `400` | `INVALID_REVIEW_ROLE` | "Target user does not have an active seller profile to receive buyer reviews." | Đánh giá sai vai trò Buyer/Seller hoặc không có `SellerProfile` được duyệt. |
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Người dùng chưa đăng nhập. |
| `404` | `USER_NOT_FOUND` | "The target user account was not found." | Không tìm thấy `revieweeId`. |
| `409` | `REVIEW_ALREADY_EXISTS` | "A review has already been submitted for this transaction reference." | Đã tồn tại đánh giá cho cùng `(ReviewerId, ReferenceType, ReferenceId)`. |

---

## 6. DANH MỤC ENUM LIÊN QUAN (DOMAIN ENUMS)

| Tên trường Payload (JSON Field) | Kiểu C# Enum | Giá trị DB (`integer`) | Giá trị API (`string PascalCase`) | Ý nghĩa nghiệp vụ |
|:---|:---|:---:|:---|:---|
| `reviewerRole` | `ReviewerRole` | `0`, `1` | `Buyer`, `Seller` | Vai trò của người đánh giá. |
| `targetRole` | `TargetRole` | `0`, `1` | `Buyer`, `Seller` | Quỹ điểm của người nhận đánh giá bị tác động (`1 sao = -2`, `2 sao = -1`, `3 sao = 0`, `4-5 sao = +1`). |
| `referenceType` | `ReferenceType` | `0`, `1` | `Order`, `Auction` | Loại giao dịch gốc. |

---

## 7. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IReviewService.cs` và `ReviewService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `ReviewsController.cs` (`POST /api/v1/reviews`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test` vượt qua 100% (`ReviewServiceTests.cs`, `ReputationAndGovernanceControllersTests.cs`).

