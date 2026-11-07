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
using User.Application.Services.Report;
using User.Domain.Enums;

/// <summary>
/// Quản lý và giải quyết báo cáo tố cáo vi phạm dành cho Admin và Support Staff.
/// </summary>
[ApiController]
[Route("api/v1/admin/reports")]
[RequireRole("admin", "staff")]
public sealed class AdminReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ILogger<AdminReportsController> _logger;

    public AdminReportsController(IReportService reportService, ILogger<AdminReportsController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>
    /// [Admin / Staff] Tra cứu danh sách báo cáo tố cáo vi phạm toàn hệ thống.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedData<ReportListItemDto>))]
    public async Task<IActionResult> GetAdminReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ReportStatus? status = null,
        [FromQuery] ReportViolationType? violationType = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        _logger.LogInformation("[REST Admin GetReports] Querying reports page={Page}, pageSize={PageSize}", page, pageSize);
        var result = await _reportService.GetAdminReportsAsync(page, pageSize, status, violationType, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Admin / Staff] Giải quyết báo cáo vi phạm và áp dụng chế tài kỷ luật.
    /// </summary>
    [HttpPut("{reportId:guid}/resolve")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResolveReportResponse))]
    public async Task<IActionResult> ResolveReport(
        [FromRoute] Guid reportId,
        [FromBody] ResolveReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var actorId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin ResolveReport] Resolving report '{ReportId}' by Actor '{ActorId}'", reportId, actorId);
        var result = await _reportService.ResolveReportAsync(reportId, request, actorId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [Admin / Staff] Bác bỏ báo cáo vi phạm không đủ bằng chứng.
    /// </summary>
    [HttpPut("{reportId:guid}/dismiss")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResolveReportResponse))]
    public async Task<IActionResult> DismissReport(
        [FromRoute] Guid reportId,
        [FromBody] DismissReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryGetUserId(out var actorId, out var authError))
        {
            return authError;
        }

        _logger.LogInformation("[REST Admin DismissReport] Dismissing report '{ReportId}' by Actor '{ActorId}'", reportId, actorId);
        var result = await _reportService.DismissReportAsync(reportId, request, actorId, cancellationToken);
        return Ok(result);
    }
}
