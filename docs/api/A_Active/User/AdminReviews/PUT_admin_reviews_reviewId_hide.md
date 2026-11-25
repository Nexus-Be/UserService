# [PUT] /api/v1/admin/reviews/{reviewId}/hide

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Review`  
> **Mã tham chiếu Excel:** `42`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `✅ VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Admin hoặc Support Staff ẩn đánh giá vi phạm tiêu chuẩn cộng đồng / oan sai (`IsDisabled = true`) và **tự động hoàn trả điểm uy tín (`-review.ScoreImpact`)** vào đúng quỹ điểm (`SellerScore` hoặc `CurrentScore`) kèm bản ghi `ReputationLog` (`Reason = DisputeCompensation`) và `AuditLog` (`AuditAction.ReviewHide`).
* **HTTP Method:** `PUT`
* **Đường dẫn chuẩn:** `/api/v1/admin/reviews/{reviewId}/hide`
* **Quyền hạn truy cập (RBAC):** `Admin`, `Staff` (`[RequireRole("admin", "staff")]`)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/AdminReviewsController.cs` $\rightarrow$ `HideReview()`
* **Application Interface:** `User.Application/Services/Review/IReviewService.cs` $\rightarrow$ `HideReviewAsync()`
* **Service Implementation:** `User.Application/Services/Review/ReviewService.cs` $\rightarrow$ `HideReviewAsync()`
* **Request / Response DTOs:** `User.Application/Services/Review/ReviewRequests.cs` (`HideReviewRequest`), `ReviewResponses.cs` (`HideReviewResponse`)
* **Thực thể & Bảng DB:** `UserReview`, `ReputationScore`, `ReputationLog`, `AuditLog`

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Route Parameters
| Tên tham số | Kiểu dữ liệu | Bắt buộc? | Ràng buộc & Định dạng |
|:---|:---|:---:|:---|
| `reviewId` | `Guid (UUID)` | Có | Mã định danh của đánh giá cần ẩn |

### 3.2. Request Body (JSON Payload)
```json
{
  "reason": "Verified unfair review during dispute moderation."
}
```

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "reviewId": "40000000-aaaa-bbbb-cccc-000000000001",
  "revieweeId": "cccccccc-3333-3333-3333-333333333333",
  "isDisabled": true,
  "targetRole": "Seller",
  "compensatedScoreDelta": 2,
  "revieweeNewScore": 50,
  "revieweeNewTrustLevel": "Standard",
  "reason": "Verified unfair review during dispute moderation.",
  "hiddenBy": "bbbbbbbb-2222-2222-2222-222222222222",
  "hiddenAt": "2026-11-07T03:00:00Z"
}
```

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `400` | `VAL_INVALID_INPUT` | "Reason for hiding the review must be at least 10 characters long." | Lý do ẩn ngắn hơn 10 ký tự. |
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Chưa đăng nhập. |
| `403` | `FORBIDDEN` | "You do not have permission to perform this action." | Không có quyền `admin` hoặc `staff`. |
| `404` | `REVIEW_NOT_FOUND` | "Review was not found for the specified identifier." | Không tìm thấy đánh giá theo `reviewId`. |
| `409` | `REVIEW_ALREADY_HIDDEN` | "This review has already been hidden by a moderator." | Đánh giá đã bị ẩn trước đó (`IsDisabled == true`). |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IReviewService.cs` và `ReviewService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `AdminReviewsController.cs` (`PUT /api/v1/admin/reviews/{reviewId}/hide`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test` vượt qua 100% (`ReviewServiceTests.cs`, `ReputationAndGovernanceControllersTests.cs`).

