# [GET] /api/v1/reputation/policy

> **Phân hệ thụ lý:** `UserService`  
> **Tên tính năng:** `Reputation`  
> **Mã tham chiếu Checklist:** `POLICY-01`  
> **Trạng thái tài liệu:** `A_Active (Đã hoàn thiện & nghiệm thu)`  
> **Trạng thái triển khai:** `✅ VERIFIED (A_Active)`  

---

## 1. MÔ TẢ NGHIỆP VỤ & PHÂN QUYỀN

* **Mô tả chức năng:** Cung cấp thông số cấu hình ngưỡng uy tín toàn sàn (`MinScoreToBid: 40`, `MinScoreToSell: 50`, mức phạt tự động, thời gian chống cày điểm 7 ngày và bảng phân hạng `TrustLevels`) cho Frontend hiển thị nhãn và kiểm tra điều kiện sớm.
* **HTTP Method:** `GET`
* **Đường dẫn chuẩn:** `/api/v1/reputation/policy`
* **Quyền hạn truy cập (RBAC):** `AllowAnonymous` (Công khai)
* **Yêu cầu Idempotency:** Không áp dụng (Idempotent theo HTTP spec)

---

## 2. DẤU VẾT MÃ NGUỒN (CODE TRACE & EXECUTION PATH)

* **Microservice thụ lý:** `UserService` (`port 8001`)
* **Controller Action:** `User.Api/Controllers/Public/ReputationController.cs` $\rightarrow$ `GetReputationPolicy()`
* **Application Interface:** `User.Application/Services/Reputation/IReputationService.cs` $\rightarrow$ `GetReputationPolicyAsync()`
* **Service Implementation:** `User.Application/Services/Reputation/ReputationService.cs` $\rightarrow$ `GetReputationPolicyAsync()`
* **Response DTO:** `User.Application/Services/Reputation/ReputationResponses.cs` $\rightarrow$ `ReputationPolicyResponse`, `ReputationDtos.cs` $\rightarrow$ `TrustLevelTierDto`
* **Thực thể & Bảng DB:** `User.Domain/Entities/SystemConfig.cs` $\rightarrow$ Bảng `SystemConfigs` trong PostgreSQL

---

## 3. PHONG BÌ DỮ LIỆU ĐẦU RA (RESPONSE PAYLOAD)

```json
200 OK:
{
  "enforcementEnabled": true,
  "initialScore": 50,
  "minScore": 0,
  "maxScore": 100,
  "minScoreToBid": 40,
  "minScoreToSell": 50,
  "paymentTimeoutPenalty": -10,
  "sellerCancelPenalty": -5,
  "orderCompletedReward": 1,
  "antiFarmingCooldownDays": 7,
  "trustLevels": [
    {
      "level": "Untrusted",
      "minScore": 0,
      "maxScore": 39,
      "description": "Restricted account: bidding and selling privileges are suspended."
    },
    {
      "level": "Probation",
      "minScore": 40,
      "maxScore": 49,
      "description": "Probation status: allowed to bid as buyer, but restricted from creating seller auctions."
    },
    {
      "level": "Standard",
      "minScore": 50,
      "maxScore": 69,
      "description": "Standard trust level: full bidding and selling privileges enabled."
    },
    {
      "level": "Good",
      "minScore": 70,
      "maxScore": 89,
      "description": "Trusted member with a consistent positive trading track record."
    },
    {
      "level": "Excellent",
      "minScore": 90,
      "maxScore": 100,
      "description": "Top-tier member eligible for priority badges and platform perks."
    }
  ]
}
```

---

## 4. CHECKLIST HOÀN THÀNH & NGHIỆM THU (COMPLETION CHECKLIST)

- [x] **1. Tầng Application:** `IReputationService.cs` và `ReputationService.cs` đã được triển khai hoàn chỉnh.
- [x] **2. Tầng Api:** `ReputationController.cs` (`GET /api/v1/reputation/policy`) đã triển khai chuẩn.
- [x] **3. Kiểm thử tự động:** `dotnet test` vượt qua 100% (`ReputationServiceTests.cs`).

