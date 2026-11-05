namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Application.Services.Profile;

/// <summary>
/// Tra cứu thông tin hồ sơ người dùng công khai (Public Profiles).
/// Không yêu cầu xác thực đăng nhập.
/// </summary>
[ApiController]
[Route("api/v1/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IProfileService profileService, ILogger<UsersController> logger)
    {
        _profileService = profileService;
        _logger = logger;
    }

    /// <summary>
    /// [Public] Tra cứu thông tin hồ sơ công khai an toàn của một người dùng bất kỳ theo ID.
    /// Không làm lộ các thông tin nhạy cảm (Email, Số điện thoại, CCCD, Mật khẩu).
    /// </summary>
    /// <param name="userId">Định danh GUID của người dùng cần xem hồ sơ.</param>
    /// <param name="cancellationToken">Token hủy yêu cầu bất đồng bộ.</param>
    /// <returns>Hồ sơ công khai: Tên hiển thị, Avatar, Điểm uy tín, Mức độ tin cậy.</returns>
    /// <response code="200">Trả về thông tin hồ sơ công khai thành công.</response>
    /// <response code="404">USER_NOT_FOUND nếu không tìm thấy người dùng.</response>
    [HttpGet("{userId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PublicUserProfileDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserPublicProfile([FromRoute] Guid userId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[REST GetUserPublicProfile] Loading public profile for UserId: '{UserId}'", userId);
        var profile = await _profileService.GetPublicProfileAsync(userId, cancellationToken);

        return Ok(profile);
    }

    /// <summary>
    /// Hàm bọc tương thích ngược phục vụ các bài kiểm thử tự động cũ.
    /// </summary>
    [NonAction]
    public Task<IActionResult> GetUserProfileById(Guid id, CancellationToken cancellationToken)
        => GetUserPublicProfile(id, cancellationToken);
}
