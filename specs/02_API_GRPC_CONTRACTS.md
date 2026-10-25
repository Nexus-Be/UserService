# 02 — HỢP ĐỒNG GIAO TIẾP GRPC (USER SERVICE GRPC CONTRACTS)
> **Trạng thái triển khai (2026-10-25):** ⚠️ Một phần — **5/13 rpc** đã override trong `UsersGrpcService` (`Register`, `VerifyOtp`, `ResendOtp`, `Login`, `GetProfile`), **8/13 rpc còn là đặc tả** và gọi vào sẽ trả `UNIMPLEMENTED`. `rpc Login` ✅ đã implement kèm phát hành JWT token (`JwtTokenGenerator`).
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-25.
Dịch vụ gRPC của UserService được định nghĩa trong hợp đồng duy nhất `User.Api/Contracts/shop.proto` (được biên dịch với `GrpcServices="Server"`) dưới service `UsersApi` (namespace sinh ra: `ShoppingCart.Contracts.V1`) — xem `User.Api/User.Api.csproj` (`<Protobuf Include="Contracts/shop.proto" GrpcServices="Server" />`).
---
## 1. DANH SÁCH CÁC RPC METHODS
```protobuf
service UsersApi {                                   // shop.proto:47-65
  rpc GetProfile(UserRequest) returns (UserProfile);            // :49  ✅
  rpc Register(RegisterRequest) returns (UserProfile);          // :51  ✅
  rpc Login(LoginRequest) returns (LoginResponse);              // :52  ✅
  rpc UpdateProfile(UpdateProfileRequest) returns (UserProfile);// :53  ❌
  rpc ListUsers(ListUsersRequest) returns (UserPage);           // :54  ❌
  rpc AssignRoles(AssignRolesRequest) returns (UserProfile);    // :55  ❌
  rpc ListRoles(Empty) returns (RoleList);                      // :56  ❌
  rpc ListPermissions(Empty) returns (PermissionList);          // :57  ❌
  rpc RequestSellerUpgrade(SellerUpgradeRequest) returns (SellerRegistrationInfo); // :58 ❌
  rpc ApproveSeller(ApproveSellerRequest) returns (SellerRegistrationInfo);        // :59 ❌
  rpc CreateUser(CreateUserRequest) returns (UserProfile);      // :61  ❌
  rpc VerifyOtp(VerifyOtpRequest) returns (UserProfile);        // :63  ✅
  rpc ResendOtp(ResendOtpRequest) returns (OtpResponse);        // :64  ✅
}
```
### 1.1 Bảng đối chiếu đặc tả ↔ code
| # | rpc | Request → Response | Trạng thái | Bằng chứng (override) |
|--:|:---|:---|:---|:---|
| 1 | `Register` | `RegisterRequest` → `UserProfile` | ✅ | `UsersGrpcService.cs:28-66` |
| 2 | `VerifyOtp` | `VerifyOtpRequest` → `UserProfile` | ✅ | `UsersGrpcService.cs:72-108` |
| 3 | `ResendOtp` | `ResendOtpRequest` → `OtpResponse` | ✅ | `UsersGrpcService.cs:110-136` |
| 4 | `Login` | `LoginRequest` → `LoginResponse` | ✅ | `UsersGrpcService.cs:138-167` |
| 5 | `GetProfile` | `UserRequest` → `UserProfile` | ✅ | `UsersGrpcService.cs:180-205` |
| 6 | `UpdateProfile` | `UpdateProfileRequest` → `UserProfile` | ❌ | Không có `override` |
| 7 | `ListUsers` | `ListUsersRequest` → `UserPage` | ❌ | Không có `override` |
| 8 | `AssignRoles` | `AssignRolesRequest` → `UserProfile` | ❌ | Không có `override` |
| 9 | `ListRoles` | `Empty` → `RoleList` | ❌ | Không có `override` |
| 10 | `ListPermissions` | `Empty` → `PermissionList` | ❌ | Không có `override` |
| 11 | `RequestSellerUpgrade` | `SellerUpgradeRequest` → `SellerRegistrationInfo` | ❌ | Không có `override` |
| 12 | `ApproveSeller` | `ApproveSellerRequest` → `SellerRegistrationInfo` | ❌ | Không có `override` |
| 13 | `CreateUser` | `CreateUserRequest` → `UserProfile` | ❌ | Không có `override` |
**Đếm thật: 5/13 ✅ · 8/13 ❌.** Gọi bất kỳ rpc ❌ nào sẽ nhận `StatusCode.Unimplemented` (HTTP/2 `UNIMPLEMENTED`, Gateway map thành 501).
### 1.2 Hành vi chi tiết của 5 rpc đã có
| rpc | Input field thực dùng | Hành vi |
|:---|:---|:---|
| `Register` | `Email`, `Password`, `FirstName`, `LastName`, `PhoneNumber`, `DisplayName`, `Username` | Xác thực `Username` (3-50 ký tự), kiểm tra trùng lặp `USERNAME_ALREADY_EXISTS`. Suy ra tên nếu thiếu. Lưu user trạng thái `PendingVerification` và gửi OTP qua RabbitMQ |
| `VerifyOtp` | `Email`, `Otp` | Gọi `AuthService.VerifyOtpAsync`, kích hoạt user thành `Active` |
| `ResendOtp` | `Email` | Gọi `AuthService.ResendOtpAsync`; gửi mã OTP mới qua email |
| `Login` | `Username` (hoặc Email), `Password` | Đăng nhập bằng Email hoặc Username; xác thực BCrypt; kiểm tra trạng thái (`USER_NOT_VERIFIED`, `USER_BANNED`); sinh JWT token kèm danh sách vai trò |
| `GetProfile` | `x-user-id` metadata **hoặc** `UserRequest.UserId` | Metadata ưu tiên hơn body (`UsersGrpcService.cs:180-205`); query kèm `UserRoles.Role` + `SellerProfile`; trả `NotFound` nếu không có user |
---
## 2. QUY CHUẨN XỬ LÝ LỖI & MÃ LỖI (ERROR STATUS CODES)
Khi xảy ra lỗi trong quá trình xử lý, `UsersGrpcService` ném `RpcException` với `Status` được map từ `DomainException.ErrorCode` qua hàm `MapDomainErrorCodeToGrpcStatus` (`UsersGrpcService.cs:169-178`).
### 2.1 Bảng map **thật** trong code
| Mã lỗi (SCREAMING_SNAKE) | gRPC `StatusCode` | Nơi phát sinh |
|:---|:---|:---|
| `USER_NOT_FOUND` | `NotFound` (404) | `AuthService.cs` |
| `EMAIL_ALREADY_EXISTS` / `USERNAME_ALREADY_EXISTS` | `AlreadyExists` (409) | `AuthService.cs` |
| `INVALID_CREDENTIALS` | `Unauthenticated` (401) | `AuthService.cs` |
| `USER_NOT_VERIFIED` / `OTP_ALREADY_USED` / `OTP_EXPIRED` / `USER_ALREADY_VERIFIED` | `FailedPrecondition` (412) | `AuthService.cs` |
| `USER_BANNED` / `USER_SUSPENDED` | `PermissionDenied` (403) | `AuthService.cs` |
| `OTP_INVALID` | `InvalidArgument` (400) | `AuthService.cs` |
| `VAL_INVALID_EMAIL` / `VAL_INVALID_PASSWORD` / `VAL_INVALID_FIRST_NAME` / `VAL_INVALID_LAST_NAME` / `VAL_INVALID_OTP` / `VAL_INVALID_USERNAME` | `InvalidArgument` (400) | `AuthService.cs` |
| Mọi `Exception` khác | `Internal` (500) | catch-all trong từng method |
### 2.2 Middleware lỗi
- `GlobalExceptionHandlerMiddleware` (`User.Api/Middleware/GlobalExceptionHandlerMiddleware.cs`) chỉ áp dụng cho **HTTP** (vd. `/health`, `/swagger`), trả `application/problem+json` với `{ type, title, status, errorCode, detail, traceId }`; `type` trỏ tới `https://tools.ietf.org/html/rfc9110#section-15.5.1` (4xx) hoặc `15.6.1` (5xx).
- Lỗi gRPC **không** đi qua middleware này mà do từng method tự `catch` và ném `RpcException`.