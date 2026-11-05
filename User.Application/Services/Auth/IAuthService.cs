namespace User.Application.Services.Auth;

using System;
using System.Threading;
using System.Threading.Tasks;
using User.Domain.Entities;

/// <summary>
/// Hợp đồng dịch vụ xác thực và quản lý phiên định danh (Authentication và Session Management).
/// Bao gồm đăng ký tài khoản mới, xác thực OTP email, đăng nhập cấp JWT, làm mới token, đăng xuất và quản lý mật khẩu.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Đăng ký tài khoản người dùng mới (vai trò mặc định: Buyer).
    /// </summary>
    /// <param name="dto">Dữ liệu đăng ký: Email, Mật khẩu, Họ tên, Username, Số điện thoại.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thực thể User vừa được tạo ở trạng thái PendingVerification.</returns>
    Task<User> RegisterAsync(RegisterDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đăng ký tài khoản qua API RESTful và gửi OTP xác thực email.
    /// </summary>
    /// <param name="request">Payload đăng ký từ client.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Phản hồi đăng ký chứa ID người dùng và thông báo gửi OTP.</returns>
    Task<RegisterApiResponse> RegisterUserAsync(RegisterApiRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đăng ký tài khoản phục vụ môi trường kiểm thử (trả về OTP trực tiếp trong response).
    /// </summary>
    /// <param name="request">Payload đăng ký kiểm thử.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Phản hồi chứa mã OTP để test tự động.</returns>
    Task<RegisterTestApiResponse> RegisterTestUserAsync(RegisterTestApiRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xác thực mã OTP để kích hoạt tài khoản người dùng (chuyển trạng thái sang Active).
    /// </summary>
    /// <param name="email">Email tài khoản cần kích hoạt.</param>
    /// <param name="otp">Mã OTP gồm 6 chữ số.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thực thể User sau khi kích hoạt thành công.</returns>
    Task<User> VerifyOtpAsync(string email, string otp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xác thực OTP qua API RESTful, kích hoạt tài khoản và khởi tạo ví thanh toán non-blocking.
    /// </summary>
    /// <param name="request">Payload chứa email và mã OTP.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Phản hồi xác thực thành công.</returns>
    Task<VerifyOtpApiResponse> VerifyUserOtpAsync(VerifyOtpApiRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi lại mã OTP xác thực email khi mã cũ hết hạn hoặc bị thất lạc.
    /// </summary>
    /// <param name="email">Email nhận mã OTP mới.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>True nếu gửi OTP thành công.</returns>
    Task<bool> ResendOtpAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xác thực thông tin đăng nhập và cấp phát cặp Access Token (JWT) và Refresh Token.
    /// </summary>
    /// <param name="dto">Dữ liệu đăng nhập: Email/Username, Mật khẩu, DeviceInfo, IP.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Token JWT chứa claim vai trò đơn nhất và Refresh Token có hạn dài.</returns>
    Task<LoginResultDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cấp mới Access Token (JWT) bằng Refresh Token hợp lệ (Token Rotation).
    /// </summary>
    /// <param name="dto">Payload chứa Refresh Token và thông tin thiết bị.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Cặp Access Token mới và Refresh Token mới.</returns>
    Task<RefreshTokenResultDto> RefreshTokenAsync(RefreshTokenDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đăng xuất và vô hiệu hóa Refresh Token trên thiết bị hiện tại.
    /// </summary>
    /// <param name="refreshToken">Mã Refresh Token cần thu hồi.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>True nếu thu hồi token thành công.</returns>
    Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đăng xuất trên tất cả thiết bị (thu hồi toàn bộ Refresh Tokens của người dùng).
    /// </summary>
    /// <param name="userId">ID người dùng cần đăng xuất toàn bộ.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Số lượng phiên đăng nhập đã bị vô hiệu hóa.</returns>
    Task<int> LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Yêu cầu quên mật khẩu và gửi OTP xác nhận đặt lại mật khẩu về email.
    /// </summary>
    /// <param name="dto">Email của tài khoản cần đặt lại mật khẩu.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Kết quả gửi OTP thành công.</returns>
    Task<ForgotPasswordResultDto> ForgotPasswordAsync(ForgotPasswordDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đặt lại mật khẩu mới thông qua mã OTP xác nhận.
    /// </summary>
    /// <param name="dto">Email, mã OTP xác nhận và mật khẩu mới.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Kết quả xác nhận mật khẩu đã được đổi thành công.</returns>
    Task<ResetPasswordResultDto> ResetPasswordAsync(ResetPasswordDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Thay đổi mật khẩu khi người dùng đang đăng nhập (yêu cầu xác thực mật khẩu cũ).
    /// </summary>
    /// <param name="userId">ID người dùng đang thực hiện đổi mật khẩu.</param>
    /// <param name="dto">Mật khẩu hiện tại và mật khẩu mới.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Kết quả đổi mật khẩu thành công.</returns>
    Task<ChangePasswordResultDto> ChangePasswordAsync(Guid userId, ChangePasswordDto dto, CancellationToken cancellationToken = default);
}
