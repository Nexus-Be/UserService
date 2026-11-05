namespace User.Application.Services.Admin;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Hợp đồng nghiệp vụ quản trị người dùng dành cho Administrator.
/// Bao gồm tạo tài khoản, phân trang, phân quyền 1-User 1-Primary-Role, cập nhật, xóa mềm và khóa/mở khóa tài khoản.
/// </summary>
public interface IAdminUserService
{
    /// <summary>
    /// Khởi tạo người dùng hoặc nhân viên mới trực tiếp bởi quản trị viên.
    /// </summary>
    /// <param name="dto">Dữ liệu tạo tài khoản (Email, Mật khẩu, Họ tên, SĐT, Vai trò chỉ định).</param>
    /// <param name="adminId">ID của quản trị viên thực hiện thao tác.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông tin người dùng mới được tạo.</returns>
    Task<AdminUserCreatedResponseDto> CreateUserAsync(
        CreateAdminUserDto dto,
        Guid? adminId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Tra cứu danh sách người dùng toàn sàn có phân trang và bộ lọc từ khóa/vai trò/trạng thái.
    /// </summary>
    /// <param name="page">Trang hiện tại (bắt đầu từ 1).</param>
    /// <param name="limit">Số bản ghi mỗi trang (tối đa 100).</param>
    /// <param name="keyword">Từ khóa tìm kiếm theo Email, Username, Họ tên, SĐT.</param>
    /// <param name="role">Lọc theo mã vai trò (buyer, seller, staff, admin).</param>
    /// <param name="status">Lọc theo trạng thái tài khoản.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Dữ liệu phân trang chứa danh sách người dùng và metadata.</returns>
    Task<PagedData<AdminUserListItemDto>> GetUsersAsync(
        int page = 1,
        int limit = 20,
        string? keyword = null,
        string? role = null,
        string? status = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Tra cứu thông tin chi tiết một tài khoản người dùng cho quản trị viên.
    /// </summary>
    /// <param name="userId">Định danh GUID của tài khoản.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ người dùng chi tiết, điểm uy tín và thông tin gian hàng nếu có.</returns>
    Task<AdminUserDetailDto> GetUserDetailAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Tra cứu danh mục các vai trò khả dụng trong hệ thống.
    /// </summary>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Danh sách các vai trò hệ thống.</returns>
    Task<AdminRoleListResponseDto> GetRolesAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Phân quyền hoặc thay đổi vai trò chính của người dùng (Tuân thủ nguyên tắc 1-User 1-Primary-Role).
    /// </summary>
    /// <param name="targetUserId">ID người dùng cần phân quyền.</param>
    /// <param name="dto">Dữ liệu phân quyền (chỉ cho phép 1 vai trò duy nhất) và lý do.</param>
    /// <param name="adminId">ID của quản trị viên thực hiện.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông tin người dùng kèm vai trò mới sau cập nhật.</returns>
    Task<AdminUserRolesResponseDto> AssignUserRolesAsync(
        Guid targetUserId,
        AssignAdminUserRolesDto dto,
        Guid? adminId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Cập nhật thông tin hồ sơ người dùng bởi quản trị viên.
    /// </summary>
    /// <param name="targetUserId">ID người dùng cần cập nhật.</param>
    /// <param name="dto">Dữ liệu thông tin cập nhật.</param>
    /// <param name="adminId">ID của quản trị viên thực hiện.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ người dùng sau khi cập nhật.</returns>
    Task<AdminUserUpdatedResponseDto> UpdateUserAsync(
        Guid targetUserId,
        UpdateAdminUserDto dto,
        Guid? adminId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Xóa mềm tài khoản người dùng và vô hiệu hóa toàn bộ phiên đăng nhập.
    /// </summary>
    /// <param name="targetUserId">ID người dùng cần xóa.</param>
    /// <param name="dto">Lý do xóa tài khoản (tùy chọn).</param>
    /// <param name="adminId">ID của quản trị viên thực hiện.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    Task DeleteUserAsync(
        Guid targetUserId,
        DeleteAdminUserDto? dto,
        Guid? adminId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Khóa tài khoản người dùng vi phạm nghiêm trọng (chặn mua, bán và đăng nhập toàn diện).
    /// </summary>
    /// <param name="targetUserId">ID người dùng cần khóa.</param>
    /// <param name="dto">Dữ liệu bắt buộc chứa lý do khóa.</param>
    /// <param name="adminId">ID của quản trị viên thực hiện.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Kết quả xác nhận tài khoản bị khóa.</returns>
    Task<AdminUserBannedResponseDto> BanUserAsync(
        Guid targetUserId,
        BanAdminUserDto dto,
        Guid? adminId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Mở khóa tài khoản đã bị đình chỉ trước đó.
    /// </summary>
    /// <param name="targetUserId">ID người dùng cần mở khóa.</param>
    /// <param name="dto">Lý do mở khóa (tùy chọn).</param>
    /// <param name="adminId">ID của quản trị viên thực hiện.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Kết quả xác nhận tài khoản hoạt động trở lại.</returns>
    Task<AdminUserUnbannedResponseDto> UnbanUserAsync(
        Guid targetUserId,
        UnbanAdminUserDto? dto = null,
        Guid? adminId = null,
        CancellationToken cancellationToken = default
    );
}
