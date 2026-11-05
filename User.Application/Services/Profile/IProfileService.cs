namespace User.Application.Services.Profile;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Hợp đồng dịch vụ quản lý hồ sơ cá nhân và hồ sơ công khai của người dùng.
/// </summary>
public interface IProfileService
{
    /// <summary>
    /// Tra cứu hồ sơ cá nhân của chính người dùng đang đăng nhập.
    /// </summary>
    /// <param name="userId">ID người dùng đang xác thực (trích xuất từ HttpContext token).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ cá nhân chi tiết bao gồm định danh, vai trò duy nhất, điểm uy tín và địa chỉ.</returns>
    Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tra cứu thông tin hồ sơ công khai an toàn của một người dùng bất kỳ (không lộ email, SĐT, CCCD).
    /// </summary>
    /// <param name="userId">ID người dùng cần tra cứu.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ công khai: Tên hiển thị, Avatar, Điểm uy tín, Mức độ tin cậy.</returns>
    Task<PublicUserProfileDto> GetPublicProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật thông tin hồ sơ cá nhân của chính mình.
    /// </summary>
    /// <param name="userId">ID người dùng đang xác thực.</param>
    /// <param name="dto">Dữ liệu cập nhật: Họ tên, SĐT, Avatar, Địa chỉ, Ngày sinh, Giới tính.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ cá nhân sau khi cập nhật thành công.</returns>
    Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileDto dto, CancellationToken cancellationToken = default);
}
