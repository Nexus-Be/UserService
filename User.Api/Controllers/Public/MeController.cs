namespace User.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Api.Extensions;
using User.Api.Filters;
using User.Application.Common.Models;
using User.Application.Services.Profile;
using User.Application.Services.Report;
using User.Application.Services.Reputation;
using User.Domain.Enums;

/// <summary>
/// Quản lý hồ sơ cá nhân của chính người dùng đang đăng nhập (Contextual Singleton /me).
/// Yêu cầu người dùng đã được xác thực hợp lệ.
/// </summary>
[ApiController]
[Route("api/v1/me")]
public sealed class MeController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly IReputationService? _reputationService;
    private readonly IReportService? _reportService;
    private readonly ILogger<MeController> _logger;

    public MeController(
        IProfileService profileService,
        ILogger<MeController> logger,
        IReputationService? reputationService = null,
        IReportService? reportService = null)
    {
        _profileService = profileService;
        _logger = logger;
        _reputationService = reputationService;
        _reportService = reportService;
    }

    /// <summary>
    /// [Authenticated] Tra cứu hồ sơ cá nhân chi tiết của chính người dùng đang đăng nhập.
    /// </summary>
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

    /// <summary>
    /// [Authenticated] Xem chi tiết điểm uy tín kép (BuyerScore và SellerScore) của chính người dùng đang đăng nhập.
    /// </summary>
    [HttpGet("reputation")]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MyReputationResponse))]
    public async Task<IActionResult> GetMyReputation(CancellationToken cancellationToken)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST GetMyReputation] Fetching dual reputation for UserId: '{UserId}'", userId);
        var result = await _reputationService!.GetMyReputationAsync(userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Authenticated] Xem danh sách các án phạt vi phạm của chính người dùng đang đăng nhập.
    /// </summary>
    [HttpGet("penalties")]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedData<MyPenaltyItemDto>))]
    public async Task<IActionResult> GetMyPenalties(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST GetMyPenalties] Fetching penalties for UserId: '{UserId}'", userId);
        var result = await _reputationService!.GetMyPenaltiesAsync(userId, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Authenticated] Xem danh sách các báo cáo tố cáo vi phạm do chính người dùng đã gửi.
    /// </summary>
    [HttpGet("reports")]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedData<ReportListItemDto>))]
    public async Task<IActionResult> GetMyReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ReportStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST GetMyReports] Fetching reports submitted by UserId: '{UserId}'", userId);
        var result = await _reportService!.GetMyReportsAsync(userId, page, pageSize, status, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Authenticated] Xem danh sách các phiên đăng nhập đang hoạt động của chính người dùng.
    /// </summary>
    [HttpGet("sessions")]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedData<ActiveSessionDto>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetActiveSessions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        var sidRaw = User?.FindFirst("sid")?.Value
            ?? Request.Headers["X-Session-Id"].FirstOrDefault();

        Guid? currentSessionId = Guid.TryParse(sidRaw, out var parsedSid)
            ? parsedSid
            : null;

        _logger.LogInformation("[REST GetActiveSessions] Fetching active sessions for UserId: '{UserId}'", userId);
        var result = await _profileService.GetActiveSessionsAsync(userId, currentSessionId, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Authenticated] Thu hồi / đăng xuất thiết bị từ xa theo Session ID cụ thể.
    /// </summary>
    [HttpDelete("sessions/{id:guid}")]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSession([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST RevokeSession] User '{UserId}' revoking session '{SessionId}'", userId, id);
        await _profileService.RevokeSessionAsync(userId, id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// [Authenticated] Tải lên và cập nhật ảnh đại diện cá nhân (JPG/PNG/WebP, tối đa 2MB).
    /// </summary>
    [HttpPost("avatar")]
    [RequireRole]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadAvatarResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> UploadMyAvatar(IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        if (file == null || file.Length <= 0)
        {
            throw new User.Domain.Exceptions.BusinessRuleValidationException("VAL_INVALID_INPUT", "Avatar image file is required.");
        }

        await using var stream = file.OpenReadStream();
        var result = await _profileService.UploadMyAvatarAsync(
            userId,
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// [Authenticated] Lấy nhanh URL ảnh đại diện cá nhân phục vụ Header UI.
    /// </summary>
    [HttpGet("avatar")]
    [RequireRole]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserAvatarResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyAvatar(CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var userId, out var authError))
        {
            return authError;
        }

        var result = await _profileService.GetUserAvatarAsync(userId, cancellationToken);
        return Ok(result);
    }
}
