namespace User.Api.Controllers.Internal;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using User.Api.Common;
using User.Application.Services.Audit;

/// <summary>Lets the central log service pull audit records that have not been synced yet.</summary>
[ApiController]
[Route("api/v1/internal/audit-logs")]
public sealed class InternalAuditLogsController : ControllerBase
{
    private readonly IAuditSyncSourceService _audit;
    private readonly IConfiguration _configuration;

    public InternalAuditLogsController(IAuditSyncSourceService audit, IConfiguration configuration)
    {
        _audit = audit;
        _configuration = configuration;
    }

    /// <summary>Lists audit records that are not synced yet, oldest first.</summary>
    [HttpGet("unsynced")]
    public async Task<IActionResult> GetUnsynced([FromQuery] int limit = 200, CancellationToken ct = default)
    {
        if (Denied() is { } denied) return denied;
        return Ok(await _audit.GetUnsyncedAsync(limit, ct));
    }

    /// <summary>Marks audit records as synced after the central log service stored them.</summary>
    [HttpPost("mark-synced")]
    public async Task<IActionResult> MarkSynced([FromBody] MarkAuditSyncedRequest request, CancellationToken ct = default)
    {
        if (Denied() is { } denied) return denied;
        return Ok(await _audit.MarkSyncedAsync(request?.Ids ?? Array.Empty<Guid>(), ct));
    }

    private IActionResult? Denied()
    {
        if (InternalServiceKeyValidator.TryValidate(Request, _configuration, out var isConfigured))
        {
            return null;
        }

        return isConfigured
            ? StatusCode(403, new { errorCode = "FORBIDDEN", message = "Access denied to internal service resource." })
            : StatusCode(500, new { errorCode = "INTERNAL_CONFIGURATION_ERROR", message = "Internal service authentication is not configured." });
    }
}
