namespace User.Application.Services.Profile;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using User.Application.Common.Models;

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

    /// <summary>
    /// Tra cứu danh sách phiên đăng nhập đang hoạt động của chính người dùng (phân trang).
    /// </summary>
    Task<PagedData<ActiveSessionDto>> GetActiveSessionsAsync(
        Guid userId,
        Guid? currentSessionId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thu hồi / đăng xuất từ xa một phiên đăng nhập theo SessionId.
    /// </summary>
    Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tải lên và cập nhật ảnh đại diện cá nhân (JPG, PNG, WebP tối đa 2MB).
    /// </summary>
    Task<UploadAvatarResponse> UploadMyAvatarAsync(
        Guid userId,
        Stream content,
        string fileName,
        string contentType,
        long fileSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy nhanh URL ảnh đại diện của một người dùng theo ID.
    /// </summary>
    Task<UserAvatarResponse> GetUserAvatarAsync(Guid userId, CancellationToken cancellationToken = default);
}
