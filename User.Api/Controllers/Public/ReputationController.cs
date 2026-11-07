namespace User.Api.Controllers;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using User.Application.Services.Reputation;

/// <summary>
/// Cung cấp bảng quy định và ngưỡng điểm uy tín công khai toàn sàn.
/// </summary>
[ApiController]
[Route("api/v1/reputation")]
public sealed class ReputationController : ControllerBase
{
    private readonly IReputationService _reputationService;
    private readonly ILogger<ReputationController> _logger;

    public ReputationController(IReputationService reputationService, ILogger<ReputationController> logger)
    {
        _reputationService = reputationService;
        _logger = logger;
    }

    /// <summary>
    /// [Public] Tra cứu chính sách điểm uy tín, ngưỡng đấu giá/bán hàng và phân hạng TrustLevel.
    /// </summary>
    [HttpGet("policy")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReputationPolicyResponse))]
    public async Task<ActionResult<ReputationPolicyResponse>> GetReputationPolicy(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[REST GetReputationPolicy] Fetching platform reputation policy");
        var result = await _reputationService.GetReputationPolicyAsync(cancellationToken);
        return Ok(result);
    }
}
