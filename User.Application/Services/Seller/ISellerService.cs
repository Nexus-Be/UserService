namespace User.Application.Services.Seller;

using System;
using System.Threading;
using System.Threading.Tasks;
using User.Application.Common.Models;

/// <summary>
/// Hợp đồng nghiệp vụ quản lý người bán (Seller).
/// Bao gồm quy trình đăng ký nâng cấp người bán, xác thực OTP email doanh nghiệp, thẩm định KYC (Support Staff),
/// quản lý hồ sơ gian hàng, tra cứu công khai, và đình chỉ/kích hoạt lại gian hàng (Staff/Admin).
/// </summary>
public interface ISellerService
{
    /// <summary>
    /// Đăng ký hồ sơ nâng cấp người bán từ tài khoản Buyer hiện tại.
    /// </summary>
    /// <param name="userId">ID người dùng đăng ký.</param>
    /// <param name="dto">Dữ liệu đăng ký: Tên shop, Email doanh nghiệp, Địa chỉ kho, CCCD, Giấy phép kinh doanh.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông tin hồ sơ đăng ký mới tạo ở trạng thái chờ xác thực hoặc chờ duyệt.</returns>
    Task<SellerRegistrationResponseDto> RegisterSellerAsync(Guid userId, CreateSellerRegistrationDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xác thực mã OTP gửi về Email doanh nghiệp để hoàn tất bước nộp đơn đăng ký.
    /// </summary>
    /// <param name="userId">ID người dùng xác thực.</param>
    /// <param name="dto">Mã OTP gồm 6 chữ số.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Kết quả xác thực OTP thành công.</returns>
    Task<VerifySellerOtpResponseDto> VerifySellerRegistrationOtpAsync(Guid userId, VerifySellerOtpDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi lại mã OTP xác thực email doanh nghiệp cho đơn đăng ký.
    /// </summary>
    /// <param name="userId">ID người dùng yêu cầu.</param>
    /// <param name="businessEmail">Email doanh nghiệp nhận OTP.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    Task ResendSellerOtpAsync(Guid userId, string businessEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tra cứu tiến trình và trạng thái đơn đăng ký người bán của chính mình.
    /// </summary>
    /// <param name="userId">ID người dùng hiện tại.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Trạng thái hồ sơ: Pending, UnderReview, Approved, Rejected.</returns>
    Task<SellerRegistrationStatusResponseDto> GetSellerRegistrationStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// [Support Staff] Tra cứu danh sách đơn đăng ký người bán đang chờ thẩm định có phân trang.
    /// </summary>
    /// <param name="page">Trang hiện tại (mặc định 1).</param>
    /// <param name="limit">Số bản ghi mỗi trang (mặc định 20).</param>
    /// <param name="status">Lọc theo trạng thái đơn đăng ký.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Danh sách đơn đăng ký kèm siêu dữ liệu phân trang.</returns>
    Task<PagedData<AdminSellerRegistrationItemDto>> GetSellerRegistrationsAsync(int page, int limit, string? status, CancellationToken cancellationToken = default);

    /// <summary>
    /// [Support Staff] Xem chi tiết hồ sơ đơn đăng ký người bán để thẩm định hồ sơ KYC.
    /// </summary>
    /// <param name="registrationId">ID của đơn đăng ký.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Chi tiết toàn bộ tài liệu KYC, hình ảnh CCCD, giấy phép kinh doanh và địa chỉ kho.</returns>
    Task<SellerRegistrationDetailsDto> GetSellerRegistrationDetailsAsync(Guid registrationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// [Support Staff] Phê duyệt đơn đăng ký người bán: đồng bộ kho hàng, chuyển vai trò Buyer -> Seller và thu hồi refresh tokens.
    /// </summary>
    /// <param name="registrationId">ID đơn đăng ký được duyệt.</param>
    /// <param name="reviewerId">ID nhân viên Support Staff duyệt đơn.</param>
    /// <param name="dto">Dữ liệu duyệt bổ sung (tùy chọn).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông tin xác nhận duyệt đơn và chuyển đổi vai trò thành công.</returns>
    Task<ApproveSellerRegistrationResponseDto> ApproveSellerRegistrationAsync(Guid registrationId, Guid? reviewerId, ApproveSellerRegistrationDto? dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// [Support Staff] Từ chối đơn đăng ký người bán do hồ sơ không hợp lệ hoặc thiếu giấy tờ.
    /// </summary>
    /// <param name="registrationId">ID đơn đăng ký bị từ chối.</param>
    /// <param name="reviewerId">ID nhân viên Support Staff từ chối.</param>
    /// <param name="dto">Payload chứa lý do từ chối (RejectionReason).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông tin xác nhận từ chối đơn đăng ký.</returns>
    Task<RejectSellerRegistrationResponseDto> RejectSellerRegistrationAsync(Guid registrationId, Guid? reviewerId, RejectSellerRegistrationDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// [Seller] Tra cứu thông tin hồ sơ gian hàng của chính mình.
    /// </summary>
    /// <param name="userId">ID người bán đang đăng nhập.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ gian hàng của người bán.</returns>
    Task<SellerOwnProfileResponseDto> GetSellerOwnProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// [Seller] Cập nhật thông tin gian hàng và địa chỉ kho lấy hàng của chính mình.
    /// </summary>
    /// <param name="userId">ID người bán đang đăng nhập.</param>
    /// <param name="dto">Dữ liệu cập nhật: Hotline, Avatar, Địa chỉ kho lấy hàng.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ gian hàng sau khi cập nhật.</returns>
    Task<SellerOwnProfileUpdatedResponseDto> UpdateSellerOwnProfileAsync(Guid userId, UpdateSellerOwnProfileDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// [Public] Tra cứu thông tin công khai của một gian hàng cho khách hàng xem shop.
    /// </summary>
    /// <param name="id">ID người bán hoặc ID gian hàng.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông tin công khai của shop: Tên shop, Avatar, Hotline, Địa chỉ tỉnh/thành, Trạng thái hoạt động.</returns>
    Task<PublicSellerProfileResponseDto> GetPublicSellerProfileAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// [Admin / Staff] Tra cứu thông tin người bán toàn diện bao gồm KYC, kho hàng và điểm uy tín cho quản trị viên/nhân viên.
    /// </summary>
    /// <param name="sellerId">ID hồ sơ người bán.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ người bán chi tiết phục vụ kiểm toán.</returns>
    Task<AdminSellerProfileResponseDto> GetAdminSellerProfileAsync(Guid sellerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// [Admin / Staff] Tạm đình chỉ quyền bán hàng của gian hàng (ShopStatus = Suspended). Quyền mua hàng của tài khoản vẫn giữ nguyên.
    /// </summary>
    /// <param name="sellerId">ID hồ sơ người bán cần đình chỉ.</param>
    /// <param name="dto">Lý do đình chỉ gian hàng.</param>
    /// <param name="adminId">ID người thực hiện (Staff hoặc Admin).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Xác nhận đình chỉ gian hàng.</returns>
    Task<SuspendSellerResponseDto> SuspendSellerAsync(Guid sellerId, SuspendSellerDto dto, Guid? adminId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// [Admin / Staff] Khôi phục quyền bán hàng cho gian hàng đã khắc phục vi phạm (ShopStatus = Active).
    /// </summary>
    /// <param name="sellerId">ID hồ sơ người bán cần kích hoạt lại.</param>
    /// <param name="dto">Lý do mở lại gian hàng (tùy chọn).</param>
    /// <param name="adminId">ID người thực hiện (Staff hoặc Admin).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Xác nhận kích hoạt lại gian hàng.</returns>
    Task<ReactivateSellerResponseDto> ReactivateSellerAsync(Guid sellerId, ReactivateSellerDto? dto = null, Guid? adminId = null, CancellationToken cancellationToken = default);
}
