namespace User.Application.Services.Report;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using User.Application.Common.Models;
using User.Domain.Enums;

public interface IReportService
{
    Task<ReportDetailResponse> CreateReportAsync(
        Guid reporterId,
        CreateReportRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedData<ReportListItemDto>> GetMyReportsAsync(
        Guid reporterId,
        int page = 1,
        int pageSize = 20,
        ReportStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<PagedData<ReportListItemDto>> GetAdminReportsAsync(
        int page = 1,
        int pageSize = 20,
        ReportStatus? status = null,
        ReportViolationType? violationType = null,
        ReportRefundStatus? refundStatus = null,
        CancellationToken cancellationToken = default);

    /// <summary>Retries the refund of a resolved report whose refund is pending or failed.</summary>
    Task<ResolveReportResponse> RetryReportRefundAsync(Guid reportId, Guid actorId, CancellationToken cancellationToken = default);

    Task<ResolveReportResponse> ResolveReportAsync(
        Guid reportId,
        ResolveReportRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<ResolveReportResponse> DismissReportAsync(
        Guid reportId,
        DismissReportRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<UploadMediaFilesResponse> UploadReportFilesAsync(
        Guid userId,
        IReadOnlyList<UploadMediaFileInput> files,
        CancellationToken cancellationToken = default);

    Task<ReportListItemDto> GetMyReportByIdAsync(
        Guid reporterId,
        Guid reportId,
        CancellationToken cancellationToken = default);

    Task<ReportListItemDto> GetReportByIdAsync(
        Guid reportId,
        CancellationToken cancellationToken = default);
}
