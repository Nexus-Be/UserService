# 02 — HỢP ĐỒNG GIAO TIẾP GRPC (USER SERVICE GRPC CONTRACTS)
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — **4/13 rpc** đã override trong `UsersGrpcService` (`Register`, `VerifyOtp`, `ResendOtp`, `GetProfile`), **9/13 rpc còn là đặc tả** và gọi vào sẽ trả `UNIMPLEMENTED`. `rpc Login` ❌ chưa implement; repo ❌ **không có code phát hành JWT**.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
Dịch vụ gRPC của UserService được định nghĩa trong hợp đồng duy nhất `User.Api/Contracts/shop.proto` (được biên dịch với `GrpcServices="Server"`) dưới service `UsersApi` (namespace sinh ra: `ShoppingCart.Contracts.V1`) — xem `User.Api/User.Api.csproj` (`<Protobuf Include="Contracts/shop.proto" GrpcServices="Server" />`).
---
## 1. DANH SÁCH CÁC RPC METHODS
```protobuf
service UsersApi {                                   // shop.proto:47-65
  rpc GetProfile(UserRequest) returns (UserProfile);            // :49  ✅
  rpc Register(RegisterRequest) returns (UserProfile);          // :51  ✅
  rpc Login(LoginRequest) returns (LoginResponse);              // :52  ❌
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
| 1 | `Register` | `RegisterRequest` → `UserProfile` | ✅ | `UsersGrpcService.cs:25` |
| 2 | `VerifyOtp` | `VerifyOtpRequest` → `UserProfile` | ✅ | `UsersGrpcService.cs:72` |
| 3 | `ResendOtp` | `ResendOtpRequest` → `OtpResponse` | ✅ | `UsersGrpcService.cs:89` |
| 4 | `GetProfile` | `UserRequest` → `UserProfile` | ✅ | `UsersGrpcService.cs:119` |
| 5 | `Login` | `LoginRequest` → `LoginResponse` | ❌ | Không có `override`. Đặc tả mục tiêu, chưa triển khai |
| 6 | `UpdateProfile` | `UpdateProfileRequest` → `UserProfile` | ❌ | Không có `override` |
| 7 | `ListUsers` | `ListUsersRequest` → `UserPage` | ❌ | Không có `override` |
| 8 | `AssignRoles` | `AssignRolesRequest` → `UserProfile` | ❌ | Không có `override` |
| 9 | `ListRoles` | `Empty` → `RoleList` | ❌ | Không có `override` |
| 10 | `ListPermissions` | `Empty` → `PermissionList` | ❌ | Không có `override` |
| 11 | `RequestSellerUpgrade` | `SellerUpgradeRequest` → `SellerRegistrationInfo` | ❌ | Không có `override` |
| 12 | `ApproveSeller` | `ApproveSellerRequest` → `SellerRegistrationInfo` | ❌ | Không có `override` |
| 13 | `CreateUser` | `CreateUserRequest` → `UserProfile` | ❌ | Không có `override` |
**Đếm thật: 4/13 ✅ · 9/13 ❌.** Gọi bất kỳ rpc ❌ nào sẽ nhận `StatusCode.Unimplemented` (HTTP/2 `UNIMPLEMENTED`, Gateway map thành 501).
### 1.2 Hành vi chi tiết của 4 rpc đã có
| rpc | Input field thực dùng | Hành vi |
|:---|:---|:---|
| `Register` | `Email`, `Password`, `FirstName`, `LastName`, `PhoneNumber`, `DisplayName`, `Username` | Nếu thiếu `FirstName` thì suy ra từ `DisplayName` (tách theo dấu cách đầu tiên) hoặc từ `Username`; `LastName` mặc định `"User"` (`UsersGrpcService.cs:27-52`). Gọi `AuthService.RegisterAsync` |
| `VerifyOtp` | `Email`, `Otp` | Gọi `AuthService.VerifyOtpAsync` |
| `ResendOtp` | `Email` | Gọi `AuthService.ResendOtpAsync`; response message cố định `"New OTP verification code has been dispatched to your email address."` |
| `GetProfile` | `x-user-id` metadata **hoặc** `UserRequest.UserId` | Metadata ưu tiên hơn body (`UsersGrpcService.cs:121`); query kèm `UserRoles.Role` + `SellerProfile`; trả `NotFound` nếu không có user |
### 1.3 `Login` — trạng thái chính xác
- Message đã có sẵn trong proto: `LoginRequest { username, password }`, `LoginResponse { access_token, expires_at, user_id, repeated roles }` (`shop.proto:225-226`).
- ❌ **Không có** method override, ❌ **không có** `LoginAsync` trong `IAuthService` (`IAuthService.cs:9-11`), ❌ **không có** bất kỳ code sinh JWT nào trong repo (grep `Jwt|JWT|GenerateToken|SecurityToken` trên `.cs` = 0; `csproj` không có `System.IdentityModel.Tokens.Jwt`/`Microsoft.AspNetCore.Authentication.JwtBearer`).
- ⚠️ `appsettings.json` có section `Jwt` (`Issuer`, `Audience`, `Signature`, `ExpirationInMinutes: 60`) nhưng **không code nào đọc** → đây là cấu hình chờ, chưa phải tính năng.
- **Hệ quả:** toàn hệ thống hiện **không thể lấy được token hợp lệ** từ bất kỳ service nào; Gateway validate JWT nhưng không có issuer thật.
---
## 2. QUY CHUẨN XỬ LÝ LỖI & MÃ LỖI (ERROR STATUS CODES)
Khi xảy ra lỗi trong quá trình xử lý, `UsersGrpcService` ném `RpcException` với `Status` được map từ `DomainException.ErrorCode` qua hàm `MapDomainErrorCodeToGrpcStatus` (`UsersGrpcService.cs:111-117`).
### 2.1 Bảng map **thật** trong code
| Mã lỗi (SCREAMING_SNAKE) | gRPC `StatusCode` | Nơi phát sinh |
|:---|:---|:---|
| `USER_NOT_FOUND` | `NotFound` (404) | `AuthService.cs:182,233` |
| `EMAIL_ALREADY_EXISTS` | `AlreadyExists` (409) | `AuthService.cs:70` |
| `OTP_ALREADY_USED` | `FailedPrecondition` (412) | `AuthService.cs:194` |
| `OTP_EXPIRED` | `FailedPrecondition` (412) | `AuthService.cs:199,204` |
| `USER_ALREADY_VERIFIED` | `FailedPrecondition` (412) | `AuthService.cs:239` |
| `OTP_INVALID` | `InvalidArgument` (400) — rơi vào nhánh `_` | `AuthService.cs:209` |
| `VAL_INVALID_EMAIL` / `VAL_INVALID_PASSWORD` / `VAL_INVALID_FIRST_NAME` / `VAL_INVALID_LAST_NAME` / `VAL_INVALID_OTP` | `InvalidArgument` (400) — nhánh `_` | `AuthService.cs:44,49,54,59,170,225` |
| Mọi `Exception` khác | `Internal` (500) | catch-all trong từng method |
### 2.2 Các mã trong đặc tả cũ **không tồn tại**
| Đặc tả cũ | Hiện trạng |
|:---|:---|
| `StatusCode.Unauthenticated` (401 — "sai tên đăng nhập hoặc mật khẩu") | ❌ **Không có** — chưa có `Login`, không chỗ nào ném `Unauthenticated` |
| `StatusCode.PermissionDenied` (403 — "không có quyền") | ❌ **Không có** — không có authorization/authentication nào trong pipeline |
| `StatusCode.InvalidArgument` cho "thiếu username" | ⚠️ Chỉ đúng một phần: `Register` không validate `Username` (chỉ dùng để suy ra tên); validate thật là Email/Password/FirstName/LastName |
### 2.3 Middleware lỗi
- `GlobalExceptionHandlerMiddleware` (`User.Api/Middleware/GlobalExceptionHandlerMiddleware.cs`) chỉ áp dụng cho **HTTP** (vd. `/health`, `/swagger`), trả `application/problem+json` với `{ type, title, status, errorCode, detail, traceId }`; `type` trỏ tới `https://tools.ietf.org/html/rfc9110#section-15.5.1` (4xx) hoặc `15.6.1` (5xx).
- Lỗi gRPC **không** đi qua middleware này mà do từng method tự `catch` và ném `RpcException`.
- Việc Gateway có map các mã này sang RFC 9110 hay không **không thể kiểm chứng từ repo này** (repo User độc lập, không đọc được code Gateway).