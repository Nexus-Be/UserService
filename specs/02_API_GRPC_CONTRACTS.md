# 02 — HỢP ĐỒNG GIAO TIẾP GRPC (USER SERVICE GRPC CONTRACTS)
> **Trạng thái triển khai (2026-10-27):** ✅ Đã đồng bộ 100% với ApiGateway theo thiết kế cốt lõi hợp nhất:
> Duy nhất hợp đồng `Contracts/shop.proto`: Định nghĩa `service UsersApi` (36 RPCs gồm 5 RPC cốt lõi + 31 RPC mở rộng về Auth, Profile, Seller, Admin) triển khai tập trung tại `UsersGrpcService`.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-27.

Dịch vụ gRPC của UserService được đăng ký duy nhất tại `User.Api/Program.cs`:
```csharp
app.MapGrpcService<UsersGrpcService>(); // Phục vụ shop.proto: UsersApi (Toàn bộ 36 RPCs)
```

---

## 1. DANH SÁCH CÁC RPC METHODS

### 1.1 `service UsersApi` (Hợp đồng hợp nhất: `Contracts/shop.proto`)
Triển khai tập trung tại `UsersGrpcService.cs`:

| # | rpc | Request → Response | Trạng thái | Bằng chứng (override) |
|--:|:---|:---|:---|:---|
| 1 | `Register` | `RegisterRequest` → `UserProfile` | ✅ | `UsersGrpcService.cs:30-69` |
| 2 | `VerifyOtp` | `VerifyOtpRequest` → `UserProfile` | ✅ | `UsersGrpcService.cs:83-103` |
| 3 | `ResendOtp` | `ResendOtpRequest` → `OtpResponse` | ✅ | `UsersGrpcService.cs:105-130` |
| 4 | `Login` | `LoginRequest` → `LoginResponse` | ✅ | `UsersGrpcService.cs:132-171` |
| 5 | `GetProfile` | `UserRequest` → `UserProfile` | ✅ | `UsersGrpcService.cs:184-226` |
| 6 | `RefreshToken` | `RefreshTokenRequest` → `RefreshTokenResponse` | ✅ | `UsersGrpcService.cs:228-266` |
| 7 | `UpdateMyProfile` | `UpdateMyProfileRequest` → `MyProfileUpdatedResponse` | ✅ | `UsersGrpcService.cs:268-326` |
| 8 | `ForgotPassword` | `ForgotPasswordRequest` → `ForgotPasswordResponse` | ✅ | `UsersGrpcService.cs:328-351` |
| 9 | `ResetPassword` | `ResetPasswordRequest` → `ResetPasswordResponse` | ✅ | `UsersGrpcService.cs:353-377` |
| 10 | `ChangePassword` | `ChangePasswordRequest` → `ChangePasswordResponse` | ✅ | `UsersGrpcService.cs:379-411` |
| 11 | `Logout` | `LogoutRequest` → `LogoutResponse` | ✅ | `UsersGrpcService.cs:413-436` |
| 12 | `LogoutAll` | `LogoutAllRequest` → `LogoutAllResponse` | ✅ | `UsersGrpcService.cs:438-469` |
| 13..36 | Các RPC Seller / Admin / Reviews | ... | ⏳ Kế thừa | Trả `StatusCode.Unimplemented` theo mặc định của gRPC base |

---

## 2. QUY CHUẨN XỬ LÝ LỖI & MÃ LỖI (ERROR STATUS CODES)
Khi xảy ra lỗi trong quá trình xử lý, các gRPC service ném `RpcException` với `Status` được map từ `DomainException.ErrorCode` qua hàm `MapDomainErrorCodeToGrpcStatus`.

### 2.1 Bảng map **thật** trong code
| Mã lỗi (SCREAMING_SNAKE) | gRPC `StatusCode` | Nơi phát sinh |
|:---|:---|:---|
| `USER_NOT_FOUND` | `NotFound` (404) | `AuthService.cs` / `ProfileService.cs` |
| `EMAIL_ALREADY_EXISTS` / `USERNAME_ALREADY_EXISTS` / `PHONE_ALREADY_EXISTS` | `AlreadyExists` (409) | `AuthService.cs` |
| `INVALID_CREDENTIALS` / `INVALID_REFRESH_TOKEN` | `Unauthenticated` (401) | `AuthService.cs` |
| `USER_NOT_VERIFIED` / `OTP_ALREADY_USED` / `OTP_EXPIRED` / `USER_ALREADY_VERIFIED` / `REFRESH_TOKEN_EXPIRED` / `REFRESH_TOKEN_REVOKED` / `VAL_SAME_PASSWORD` | `FailedPrecondition` (412) | `AuthService.cs` |
| `USER_BANNED` / `USER_SUSPENDED` | `PermissionDenied` (403) | `AuthService.cs` |
| `OTP_INVALID` | `InvalidArgument` (400) | `AuthService.cs` |
| `VAL_INVALID_EMAIL` / `VAL_INVALID_PASSWORD` / `VAL_INVALID_FIRST_NAME` / `VAL_INVALID_LAST_NAME` / `VAL_INVALID_OTP` / `VAL_INVALID_USERNAME` / `VAL_INVALID_INPUT` | `InvalidArgument` (400) | `AuthService.cs` |
| Mọi `Exception` khác | `Internal` (500) | catch-all trong từng method |

### 2.2 Middleware lỗi
- `GlobalExceptionHandlerMiddleware` (`User.Api/Middleware/GlobalExceptionHandlerMiddleware.cs`) chỉ áp dụng cho **HTTP** (vd. `/health`, `/swagger`), trả `application/problem+json` với `{ type, title, status, errorCode, detail, traceId }`.
- Lỗi gRPC **không** đi qua middleware này mà do từng method tự `catch` và ném `RpcException`.