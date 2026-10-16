# 02 — HỢP ĐỒNG GIAO TIẾP GRPC (USER SERVICE GRPC CONTRACTS)

Dịch vụ gRPC của UserService được định nghĩa trong hợp đồng duy nhất `Contracts/shop.proto` dưới service `UsersApi`:

---

## 1. DANH SÁCH CÁC RPC METHODS

```protobuf
service UsersApi {
  // Xác thực & Tài khoản
  rpc Register(RegisterRequest) returns (UserProfile);
  rpc Login(LoginRequest) returns (LoginResponse);
  
  // Hồ sơ cá nhân
  rpc GetProfile(UserRequest) returns (UserProfile);
  rpc UpdateProfile(UpdateProfileRequest) returns (UserProfile);
  
  // Nâng cấp & Xét duyệt người bán (Seller)
  rpc RequestSellerUpgrade(SellerUpgradeRequest) returns (SellerRegistrationInfo);
  rpc ApproveSeller(ApproveSellerRequest) returns (SellerRegistrationInfo);
  
  // Quản trị người dùng & Phân quyền (Admin/Staff)
  rpc ListUsers(ListUsersRequest) returns (UserPage);
  rpc CreateUser(CreateUserRequest) returns (UserProfile);
  rpc AssignRoles(AssignRolesRequest) returns (UserProfile);
  rpc ListRoles(Empty) returns (RoleList);
  rpc ListPermissions(Empty) returns (PermissionList);
}
```

---

## 2. QUY CHUẨN XỬ LÝ LỖI & MÃ LỖI (ERROR STATUS CODES)

Khi xảy ra lỗi trong quá trình xử lý, `UsersGrpcService` ném ra ngoại lệ `RpcException` với mã trạng thái gRPC tiêu chuẩn:
- **`StatusCode.InvalidArgument`** (400 Bad Request): Dữ liệu không hợp lệ (email sai định dạng, mật khẩu < 8 ký tự, thiếu username).
- **`StatusCode.Unauthenticated`** (401 Unauthorized): Sai tên đăng nhập hoặc mật khẩu.
- **`StatusCode.PermissionDenied`** (403 Forbidden): Không có quyền thực hiện hành động.
- **`StatusCode.NotFound`** (404 Not Found): Không tìm thấy User với ID yêu cầu.
- **`StatusCode.AlreadyExists`** (409 Conflict): Username hoặc Email đã tồn tại trong hệ thống.
- **`StatusCode.Internal`** (500 Internal Error): Lỗi hệ thống bất ngờ.

GatewayExceptionHandler tại ApiGateway sẽ tự động bắt các mã lỗi này và map sang JSON chuẩn RFC 9110 cho Client.
