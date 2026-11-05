namespace User.Api.Controllers;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Api.Filters;
using User.Application.Services.Profile;

/// <summary>
/// Quản lý hồ sơ cá nhân của chính người dùng đang đăng nhập (Contextual Singleton /me).
/// Yêu cầu người dùng đã được xác thực hợp lệ.
/// </summary>
[ApiController]
[Route("api/v1/me")]
public sealed class MeController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly ILogger<MeController> _logger;

    public MeController(IProfileService profileService, ILogger<MeController> logger)
    {
        _profileService = profileService;
        _logger = logger;
    }

    /// <summary>
    /// [Authenticated] Tra cứu hồ sơ cá nhân chi tiết của chính người dùng đang đăng nhập.
    /// </summary>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ cá nhân bao gồm ID, Email, Họ tên, SĐT, CCCD, Vai trò duy nhất và Điểm uy tín.</returns>
    /// <response code="200">Trả về hồ sơ cá nhân thành công.</response>
    /// <response code="401">UNAUTHORIZED nếu thiếu hoặc token không hợp lệ.</response>
    /// <response code="404">USER_NOT_FOUND nếu không tìm thấy người dùng.</response>
    [HttpGet]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserProfileDto))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUserProfile(CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST GetProfile] Loading profile for UserId: '{UserId}'", userId);
        var profile = await _profileService.GetProfileAsync(userId, cancellationToken);

        return Ok(profile);
    }

    /// <summary>
    /// [Authenticated] Cập nhật thông tin hồ sơ cá nhân của chính người dùng đang đăng nhập.
    /// </summary>
    /// <param name="request">Payload chứa thông tin cần cập nhật: FirstName, LastName, PhoneNumber, AvatarUrl, DateOfBirth, Gender, Address.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ cá nhân sau khi được cập nhật thành công.</returns>
    /// <response code="200">Cập nhật hồ sơ thành công.</response>
    /// <response code="400">VAL_INVALID_INPUT nếu dữ liệu đầu vào không hợp lệ.</response>
    /// <response code="401">UNAUTHORIZED nếu chưa xác thực.</response>
    /// <response code="404">USER_NOT_FOUND nếu không tìm thấy người dùng.</response>
    [HttpPatch]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserProfileDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCurrentUserProfile([FromBody] UpdateProfileApiRequest request, CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST UpdateProfile] Updating profile for UserId: '{UserId}'", userId);
        var updatedProfile = await _profileService.UpdateProfileAsync(userId, request, cancellationToken);

        return Ok(updatedProfile);
    }
}
