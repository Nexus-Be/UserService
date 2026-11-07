namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Api.Filters;
using User.Application.Common;
using User.Application.Services.Admin;
using User.Application.Services.Reputation;
using User.Application.Common.Models;

/// <summary>
/// Quản trị tài khoản người dùng nội bộ và hệ thống dành riêng cho Administrator.
/// Toàn bộ endpoint yêu cầu quyền [RequireRole("admin")].
/// </summary>
[ApiController]
[Route("api/v1/admin/users")]
[RequireRole("admin")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;
    private readonly IReputationService? _reputationService;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(
        IAdminUserService adminUserService,
        ILogger<AdminUsersController> logger,
        IReputationService? reputationService = null)
    {
        _adminUserService = adminUserService ?? throw new ArgumentNullException(nameof(adminUserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _reputationService = reputationService;
    }

    /// <summary>
    /// [Admin] Khởi tạo tài khoản người dùng hoặc nhân viên mới trực tiếp bởi quản trị viên.
    /// </summary>
    /// <param name="request">Payload chứa thông tin tài khoản: Email, Password, Họ tên, SĐT, Username và vai trò chỉ định.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông tin người dùng mới được tạo kèm vai trò và ID định danh.</returns>
    /// <response code="201">Tạo người dùng thành công.</response>
    /// <response code="400">VAL_INVALID_INPUT, SOD_VIOLATION hoặc MULTIPLE_ROLES_NOT_ALLOWED nếu gửi nhiều hơn 1 vai trò.</response>
    /// <response code="403">CANNOT_CREATE_SELLER nếu cố tình tạo trực tiếp tài khoản người bán.</response>
    /// <response code="409">EMAIL_ALREADY_EXISTS hoặc USERNAME_ALREADY_EXISTS nếu thông tin đã tồn tại.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(AdminUserCreatedResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateAdminUserDto request,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var adminId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin CreateUser] Incoming request for Email: '{Email}'", request?.Email);

        var result = await _adminUserService.CreateUserAsync(request!, adminId, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// [Admin] Tra cứu danh sách tài khoản người dùng toàn sàn có phân trang và bộ lọc linh hoạt.
    /// </summary>
    /// <param name="pagination">Thông số phân trang chuẩn: Page (mặc định 1) và PageSize (mặc định 10, tối đa 100).</param>
    /// <param name="keyword">Từ khóa tìm kiếm theo Email, Username, Họ tên hoặc Số điện thoại.</param>
    /// <param name="role">Lọc theo mã vai trò: buyer, seller, staff, admin.</param>
    /// <param name="status">Lọc theo trạng thái tài khoản: Active, PendingVerification, Banned, Deleted.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Danh sách người dùng kèm siêu dữ liệu phân trang (Items, Total, Page, PageSize, TotalPages).</returns>
    /// <response code="200">Trả về danh sách người dùng thành công.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedData<AdminUserListItemDto>))]
    public async Task<IActionResult> ListUsers(
        [FromQuery] PageQuery pagination,
        [FromQuery] string? keyword = null,
        [FromQuery] string? role = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = pagination.Normalize();

        _logger.LogInformation("[REST Admin ListUsers] Page: {Page}, PageSize: {PageSize}, Keyword: '{Keyword}', Role: '{Role}', Status: '{Status}'",
            safePage, safePageSize, keyword, role, status);

        var result = await _adminUserService.GetUsersAsync(safePage, safePageSize, keyword, role, status, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// [Admin] Tra cứu thông tin hồ sơ chi tiết và toàn diện của một người dùng cụ thể.
    /// </summary>
    /// <param name="userId">Định danh GUID của tài khoản cần tra cứu.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ người dùng chi tiết bao gồm định danh, vai trò duy nhất, điểm uy tín và thông tin gian hàng (nếu là Seller).</returns>
    /// <response code="200">Tìm thấy và trả về thông tin chi tiết người dùng.</response>
    /// <response code="404">USER_NOT_FOUND nếu không tìm thấy người dùng.</response>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminUserDetailDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserDetail(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[REST Admin GetUserDetail] Incoming request for UserId: '{UserId}'", userId);

        var result = await _adminUserService.GetUserDetailAsync(userId, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// [Admin] Phân quyền hoặc thay đổi vai trò chính của tài khoản người dùng (Enforce 1-User 1-Primary-Role).
    /// </summary>
    /// <param name="userId">Định danh GUID của tài khoản cần phân quyền.</param>
    /// <param name="request">Payload chứa RoleCodes/RoleIds (chỉ được truyền 1 role duy nhất) và lý do phân quyền.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông tin người dùng kèm vai trò mới sau khi cập nhật thành công.</returns>
    /// <response code="200">Cập nhật vai trò thành công, tự động thu hồi toàn bộ Refresh Tokens cũ.</response>
    /// <response code="400">VAL_INVALID_INPUT, SOD_VIOLATION, hoặc MULTIPLE_ROLES_NOT_ALLOWED.</response>
    /// <response code="403">CANNOT_ASSIGN_SELLER_ROLE_DIRECTLY (phải qua duyệt KYC) hoặc CANNOT_REVOKE_SELLER_ROLE_DIRECTLY (phải đình chỉ shop trước).</response>
    /// <response code="404">USER_NOT_FOUND.</response>
    [HttpPut("{userId:guid}/roles")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminUserRolesResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignUserRoles(
        [FromRoute] Guid userId,
        [FromBody] AssignAdminUserRolesDto request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var adminId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin AssignRoles] Incoming request for UserId: '{UserId}'", userId);

        var result = await _adminUserService.AssignUserRolesAsync(userId, request, adminId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Cập nhật thông tin hồ sơ người dùng bởi quản trị viên.
    /// </summary>
    /// <param name="userId">Định danh GUID của tài khoản cần cập nhật.</param>
    /// <param name="request">Payload chứa thông tin cập nhật: Họ tên, SĐT, Số CCCD, Giới tính, Ngày sinh, Địa chỉ.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông tin người dùng sau khi cập nhật.</returns>
    /// <response code="200">Cập nhật hồ sơ thành công.</response>
    /// <response code="400">VAL_INVALID_INPUT nếu dữ liệu đầu vào không hợp lệ.</response>
    /// <response code="404">USER_NOT_FOUND nếu không tìm thấy tài khoản.</response>
    [HttpPut("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminUserUpdatedResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser(
        [FromRoute] Guid userId,
        [FromBody] UpdateAdminUserDto request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var adminId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin UpdateUser] Incoming request for UserId: '{UserId}'", userId);

        var result = await _adminUserService.UpdateUserAsync(userId, request, adminId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Xóa mềm tài khoản người dùng và thu hồi toàn bộ phiên đăng nhập.
    /// </summary>
    /// <param name="userId">Định danh GUID của tài khoản cần xóa mềm.</param>
    /// <param name="request">Payload tùy chọn chứa lý do xóa tài khoản.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Không có nội dung (HTTP 204 No Content).</returns>
    /// <response code="204">Xóa mềm tài khoản thành công.</response>
    /// <response code="400">CANNOT_DELETE_SELF hoặc CANNOT_DELETE_LAST_ADMIN.</response>
    /// <response code="404">USER_NOT_FOUND nếu không tìm thấy tài khoản.</response>
    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteUser(
        [FromRoute] Guid userId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] DeleteAdminUserDto? request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var adminId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin DeleteUser] Incoming request for UserId: '{UserId}'", userId);

        await _adminUserService.DeleteUserAsync(userId, request, adminId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// [Admin] Khóa tài khoản người dùng vi phạm nghiêm trọng quy chế sàn (chặn mua, bán và đăng nhập toàn diện).
    /// </summary>
    /// <param name="userId">Định danh GUID của tài khoản cần khóa.</param>
    /// <param name="request">Payload bắt buộc chứa lý do khóa tài khoản (BanReason).</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông tin trạng thái khóa của người dùng.</returns>
    /// <response code="200">Khóa tài khoản thành công, ngắt toàn bộ phiên đăng nhập và gửi email thông báo.</response>
    /// <response code="400">VAL_INVALID_INPUT, CANNOT_BAN_SELF hoặc CANNOT_BAN_LAST_ADMIN.</response>
    /// <response code="404">USER_NOT_FOUND nếu không tìm thấy tài khoản.</response>
    [HttpPatch("{userId:guid}/ban")]
    [RequireRole("admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminUserBannedResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BanUser(
        [FromRoute] Guid userId,
        [FromBody] BanAdminUserDto request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var adminId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin BanUser] Incoming request to ban UserId: '{UserId}' by Admin: '{AdminId}'", userId, adminId);

        var result = await _adminUserService.BanUserAsync(userId, request, adminId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Mở khóa tài khoản đã bị đình chỉ trước đó.
    /// </summary>
    /// <param name="userId">Định danh GUID của tài khoản cần mở khóa.</param>
    /// <param name="request">Payload tùy chọn chứa lý do mở khóa.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Thông tin trạng thái hoạt động mới của tài khoản.</returns>
    /// <response code="200">Mở khóa tài khoản thành công và gửi email thông báo.</response>
    /// <response code="400">VAL_INVALID_INPUT hoặc tài khoản chưa từng bị khóa.</response>
    /// <response code="404">USER_NOT_FOUND nếu không tìm thấy tài khoản.</response>
    [HttpPatch("{userId:guid}/unban")]
    [RequireRole("admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminUserUnbannedResponseDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnbanUser(
        [FromRoute] Guid userId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] UnbanAdminUserDto? request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var adminId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin UnbanUser] Incoming request to unban UserId: '{UserId}' by Admin: '{AdminId}'", userId, adminId);

        var result = await _adminUserService.UnbanUserAsync(userId, request, adminId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Điều chỉnh điểm uy tín thủ công cho quỹ điểm Buyer hoặc Seller của một người dùng.
    /// </summary>
    [HttpPatch("{userId:guid}/reputation")]
    [RequireRole("admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdjustReputationResponse))]
    public async Task<IActionResult> AdjustUserReputation(
        [FromRoute] Guid userId,
        [FromBody] AdjustReputationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var adminId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin AdjustReputation] Adjusting reputation for UserId: '{UserId}' by Admin: '{AdminId}'", userId, adminId);
        var result = await _reputationService!.AdjustReputationAsync(userId, request, adminId, cancellationToken);
        return Ok(result);
    }
}
