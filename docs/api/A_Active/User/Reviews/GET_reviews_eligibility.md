# [GET] /api/v1/reviews/eligibility

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Review`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Kiểm tra xem người dùng hiện tại có đủ điều kiện để đánh giá đối tác cho một đơn hàng (`Order`) hoặc đơn đấu giá (`Auction`) hay không, và đã đánh giá trước đó chưa.
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/reviews/eligibility`
* **Quyền hạn truy cập (RBAC):** `Authenticated` (`[RequireRole]`)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/ReviewsController.cs` $\rightarrow$ `GetEligibility()`
* **Application Interface:** `User.Application/Services/Review/IReviewService.cs` $\rightarrow$ `GetEligibilityAsync()`
* **Service Implementation:** `User.Application/Services/Review/ReviewService.cs` $\rightarrow$ `GetEligibilityAsync()`
* **Response DTO:** `User.Application/Services/Review/ReviewDtos.cs` $\rightarrow$ `ReviewEligibilityResponse`
* **Thực thể & Bảng DB:** `User.Domain/Entities/UserReview.cs` $\rightarrow$ Bảng `UserReviews` trong PostgreSQL

---

## 3. QUY CHUẨN VALIDATION & THAM SỐ ĐẦU VÀO

### 3.1. Query Parameters
| Tên tham số | Kiểu dữ liệu | Bắt buộc | Ràng buộc & Mô tả |
|:---|:---|:---:|:---|
| `referenceType` | `ReferenceType` | Có | Loại giao dịch (`Order`, `Auction`). |
| `referenceId` | `string` | Có | Mã đơn hàng / đơn đấu giá tham chiếu. |

---

## 4. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "canReview": true,
  "alreadyReviewed": false,
  "reason": null,
  "revieweeId": "cccccccc-3333-3333-3333-333333333333",
  "reviewerRole": "Buyer",
  "expiresAt": "2026-12-01T10:00:00Z"
}
```
* Khi `canReview = false`, trường `reason` trả về một trong các mã: `ALREADY_REVIEWED`, `ORDER_NOT_COMPLETED`, `PERIOD_EXPIRED`, `NOT_PARTICIPANT`, hoặc `ORDER_NOT_FOUND`.

---

## 5. MA TRẬN MÃ LỖI (RFC 9110 ERROR MATRIX)

| HTTP Status | errorCode (SCREAMING_SNAKE_CASE) | message (100% English Sentence case) | Điều kiện kích hoạt trong Code Function |
|:---:|:---|:---|:---|
| `400` | `VAL_INVALID_INPUT` | "One or more validation errors occurred." | Thiếu `referenceType` hoặc `referenceId` không hợp lệ. |
| `401` | `UNAUTHORIZED` | "Authentication is required to access this resource." | Thiếu hoặc token không hợp lệ. |

---

## 6. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IReviewService.cs` và `ReviewService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `ReviewsController.cs` (`GET /api/v1/reviews/eligibility`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test User.sln -c Release` vượt qua 100%.
