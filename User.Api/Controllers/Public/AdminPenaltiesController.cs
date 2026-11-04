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
using User.Application.Services.Admin;
using User.Domain.Enums;

[ApiController]
[Route("api/v1/admin/penalties")]
[RequireRole("admin", "staff")]
public sealed class AdminPenaltiesController : ControllerBase
{
    private readonly IAdminPenaltyService _penaltyService;
    private readonly ILogger<AdminPenaltiesController> _logger;

    public AdminPenaltiesController(
        IAdminPenaltyService penaltyService,
        ILogger<AdminPenaltiesController> logger)
    {
        _penaltyService = penaltyService ?? throw new ArgumentNullException(nameof(penaltyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>[Admin / Staff] Retrieves paginated penalties ledger.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedData<AdminPenaltyListItemDto>))]
    public async Task<IActionResult> GetPenalties(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] Guid? userId = null,
        [FromQuery] TargetRole? targetRole = null,
        [FromQuery] PenaltyStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[REST Admin GetPenalties] page={Page}, limit={Limit}, userId={UserId}, targetRole={Role}, status={Status}",
            page, limit, userId, targetRole, status);

        var result = await _penaltyService.GetPenaltiesAsync(page, limit, userId, targetRole, status, cancellationToken);
        return Ok(result);
    }

    /// <summary>[Admin / Staff] Retrieves detailed penalty record including evidence and revocation info.</summary>
    [HttpGet("{penaltyId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminPenaltyDetailDto))]
    public async Task<IActionResult> GetPenaltyDetail(
        [FromRoute] Guid penaltyId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[REST Admin GetPenaltyDetail] penaltyId={PenaltyId}", penaltyId);
        var result = await _penaltyService.GetPenaltyDetailAsync(penaltyId, cancellationToken);
        return Ok(result);
    }

    /// <summary>[Admin] Revokes an active penalty and restores operational projections.</summary>
    [HttpPut("{penaltyId:guid}/revoke")]
    [RequireRole("admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AdminPenaltyRevokedResponseDto))]
    public async Task<IActionResult> RevokePenalty(
        [FromRoute] Guid penaltyId,
        [FromBody] RevokePenaltyDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var adminId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin RevokePenalty] penaltyId={PenaltyId}, adminId={AdminId}", penaltyId, adminId);
        var result = await _penaltyService.RevokePenaltyAsync(penaltyId, dto, adminId, cancellationToken);
        return Ok(result);
    }
}
