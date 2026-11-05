namespace User.Application.Services.Admin;

using System;
using System.Threading;
using System.Threading.Tasks;
using User.Application.Common.Models;
using User.Domain.Enums;

/// <summary>
/// Hợp đồng dịch vụ quản lý án phạt và kiểm tra quyền hành động theo vai trò mục tiêu (Sanctions và Permissions).
/// Phục vụ tra cứu án phạt, thu hồi án phạt và kiểm tra quyền mua/bán thời gian thực cho Gateway/OrderService.
/// </summary>
public interface IAdminPenaltyService
{
    /// <summary>
    /// Tra cứu danh sách các biên bản xử phạt vi phạm có phân trang và bộ lọc.
    /// </summary>
    /// <param name="page">Trang hiện tại (bắt đầu từ 1).</param>
    /// <param name="limit">Số bản ghi mỗi trang (mặc định 20).</param>
    /// <param name="userId">Lọc theo ID người dùng chịu phạt.</param>
    /// <param name="targetRole">Lọc theo vai trò mục tiêu bị phạt: Buyer, Seller, hoặc Account.</param>
    /// <param name="status">Lọc theo trạng thái án phạt: Active, Expired, Revoked.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Dữ liệu phân trang danh sách án phạt kèm metadata.</returns>
    Task<PagedData<AdminPenaltyListItemDto>> GetPenaltiesAsync(
        int page = 1,
        int limit = 20,
        Guid? userId = null,
        TargetRole? targetRole = null,
        PenaltyStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tra cứu chi tiết một biên bản xử phạt vi phạm.
    /// </summary>
    /// <param name="penaltyId">ID định danh biên bản xử phạt.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Chi tiết hành vi vi phạm, loại hình phạt, thời hạn và điểm uy tín bị trừ.</returns>
    Task<AdminPenaltyDetailDto> GetPenaltyDetailAsync(
        Guid penaltyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thu hồi hoặc xóa bỏ án phạt cho người dùng trước thời hạn (do giải trình thành công hoặc khiếu nại đúng).
    /// </summary>
    /// <param name="penaltyId">ID án phạt cần thu hồi.</param>
    /// <param name="dto">Payload chứa lý do thu hồi án phạt.</param>
    /// <param name="adminId">ID người thực hiện thu hồi.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Xác nhận thu hồi án phạt thành công.</returns>
    Task<AdminPenaltyRevokedResponseDto> RevokePenaltyAsync(
        Guid penaltyId,
        RevokePenaltyDto dto,
        Guid adminId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra quyền thực hiện một hành động cụ thể (Buy hoặc Sell) của người dùng theo thời gian thực.
    /// </summary>
    /// <param name="userId">ID người dùng cần kiểm tra.</param>
    /// <param name="action">Hành động cần kiểm tra: "Buy" (đặt hàng) hoặc "Sell" (đăng bán).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Kết quả cho phép hoặc bị chặn, lý do chặn và thời hạn hết hạn án phạt gần nhất.</returns>
    Task<UserActionPermissionCheckResponseDto> CheckUserActionPermissionAsync(
        Guid userId,
        string action,
        CancellationToken cancellationToken = default);
}
