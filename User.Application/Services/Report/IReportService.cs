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
        CancellationToken cancellationToken = default);

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
}
