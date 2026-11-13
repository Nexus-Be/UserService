namespace User.Application.Services.Report;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using User.Application.Common;
using User.Application.Common.Extensions;
using User.Application.Common.Helpers;
using User.Application.Common.Models;
using User.Application.Interfaces;
using User.Domain.Constants;
using User.Domain.Entities;
using User.Domain.Enums;
using User.Domain.Exceptions;

public sealed class ReportService : IReportService
{
    private readonly IUserDbContext _dbContext;
    private readonly ILogger<ReportService> _logger;
    private readonly IFileStorageService? _fileStorageService;

    public ReportService(
        IUserDbContext dbContext,
        ILogger<ReportService> logger,
        IFileStorageService? fileStorageService = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _fileStorageService = fileStorageService;
    }

    public async Task<ReportDetailResponse> CreateReportAsync(
        Guid reporterId,
        CreateReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (reporterId == request.ReportedUserId)
        {
            throw new DomainException("SELF_REPORT_NOT_ALLOWED", "You cannot submit a violation report against your own account.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length < 10)
        {
            throw new DomainException("VAL_INVALID_INPUT", "Report description must be at least 10 characters long.", 400);
        }

        var reportedUserExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.ReportedUserId, cancellationToken);

        if (!reportedUserExists)
        {
            throw new DomainException("USER_NOT_FOUND", "Reported user account was not found.", 404);
        }

        var normalizedRefId = string.IsNullOrWhiteSpace(request.ReferenceId) ? null : request.ReferenceId.Trim();

        var duplicatePending = await _dbContext.UserReports
            .AsNoTracking()
            .AnyAsync(r =>
                r.ReporterId == reporterId &&
                r.ReportedUserId == request.ReportedUserId &&
                r.ReferenceType == request.ReferenceType &&
                r.ReferenceId == normalizedRefId &&
                (r.Status == ReportStatus.Pending || r.Status == ReportStatus.UnderReview),
                cancellationToken);

        if (duplicatePending)
        {
            throw new DomainException("DUPLICATE_REPORT", "An active report already exists for this user and transaction reference.", 409);
        }

        var evidence = request.EvidenceUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).Take(5).ToList() ?? new List<string>();
        var now = DateTimeOffset.UtcNow;

        var report = new UserReport
        {
            Id = Guid.NewGuid(),
            ReporterId = reporterId,
            ReportedUserId = request.ReportedUserId,
            TargetRole = request.TargetRole,
            ReferenceType = request.ReferenceType,
            ReferenceId = normalizedRefId,
            ViolationType = request.ViolationType,
            Description = request.Description.Trim(),
            EvidenceUrls = evidence,
            Status = ReportStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.UserReports.Add(report);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ReportService] Report '{ReportId}' created by '{ReporterId}' against '{ReportedUserId}'",
            report.Id, reporterId, request.ReportedUserId);

        return new ReportDetailResponse
        {
            ReportId = report.Id,
            ReporterId = report.ReporterId,
            ReportedUserId = report.ReportedUserId,
            TargetRole = report.TargetRole.ToEnumString(),
            ReferenceType = report.ReferenceType.HasValue ? report.ReferenceType.Value.ToEnumString() : null,
            ReferenceId = report.ReferenceId,
            ViolationType = report.ViolationType.ToEnumString(),
            Description = report.Description,
            EvidenceUrls = evidence,
            Status = report.Status.ToEnumString(),
            CreatedAt = report.CreatedAt
        };
    }

    public async Task<PagedData<ReportListItemDto>> GetMyReportsAsync(
        Guid reporterId,
        int page = 1,
        int pageSize = 20,
        ReportStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safeLimit) = PaginationConstants.Normalize(page, pageSize);

        var query = _dbContext.UserReports
            .AsNoTracking()
            .Where(r => r.ReporterId == reporterId);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        return await ExecutePagedReportQueryAsync(query, safePage, safeLimit, cancellationToken);
    }

    public async Task<PagedData<ReportListItemDto>> GetAdminReportsAsync(
        int page = 1,
        int pageSize = 20,
        ReportStatus? status = null,
        ReportViolationType? violationType = null,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safeLimit) = PaginationConstants.Normalize(page, pageSize);

        var query = _dbContext.UserReports.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (violationType.HasValue)
        {
            query = query.Where(r => r.ViolationType == violationType.Value);
        }

        return await ExecutePagedReportQueryAsync(query, safePage, safeLimit, cancellationToken);
    }

    public async Task<ResolveReportResponse> ResolveReportAsync(
        Guid reportId,
        ResolveReportRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ResolutionNote) || request.ResolutionNote.Trim().Length < 10)
        {
            throw new DomainException("VAL_INVALID_INPUT", "Resolution note must be at least 10 characters long.", 400);
        }

        var report = await _dbContext.UserReports
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);

        if (report == null)
        {
            throw new DomainException("REPORT_NOT_FOUND", "User report was not found for the specified identifier.", 404);
        }

        if (report.Status == ReportStatus.Resolved || report.Status == ReportStatus.Rejected)
        {
            throw new DomainException("REPORT_ALREADY_PROCESSED", "This report has already been resolved or dismissed.", 409);
        }

        var reportedUser = await _dbContext.Users
            .Include(u => u.ReputationScore)
            .Include(u => u.SellerProfile)
            .FirstOrDefaultAsync(u => u.Id == report.ReportedUserId, cancellationToken);

        if (reportedUser == null)
        {
            throw new DomainException("USER_NOT_FOUND", "Reported user account was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        var effectiveTargetRole = request.TargetRole != TargetRole.Account ? request.TargetRole : report.TargetRole;

        report.Status = ReportStatus.Resolved;
        report.TargetRole = effectiveTargetRole;
        report.ResolvedBy = actorId;
        report.ResolvedAt = now;
        report.ResolutionNote = request.ResolutionNote.Trim();
        report.UpdatedAt = now;

        int? updatedScore = null;
        var actualDeducted = 0;

        if (request.ScoreDeduction > 0)
        {
            var repScore = reportedUser.ReputationScore;
            if (repScore == null)
            {
                repScore = ReputationScore.CreateDefault(reportedUser.Id);
                _dbContext.ReputationScores.Add(repScore);
            }

            var scoreTrack = effectiveTargetRole == TargetRole.Seller ? TargetRole.Seller : TargetRole.Buyer;
            var (_, newScore, appliedDelta) = repScore.ApplyScoreChange(
                scoreTrack,
                -Math.Abs(request.ScoreDeduction),
                isPenalty: true);

            updatedScore = newScore;
            actualDeducted = Math.Abs(appliedDelta);

            _dbContext.ReputationLogs.Add(new ReputationLog
            {
                Id = Guid.NewGuid(),
                UserId = reportedUser.Id,
                TargetRole = scoreTrack,
                ChangeAmount = appliedDelta,
                ScoreAfter = newScore,
                Reason = ReputationChangeReason.ReportPenalty,
                ReferenceType = report.ReferenceType ?? ReferenceType.Dispute,
                ReferenceId = report.ReferenceId ?? report.Id.ToString(),
                PartnerUserId = report.ReporterId,
                Note = request.ResolutionNote.Trim(),
                CreatedBy = actorId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (request.BanAccount || request.PenaltyApplied == PenaltyType.BanAccount)
        {
            reportedUser.Status = UserStatus.Banned;
            reportedUser.UpdatedAt = now;
            if (reportedUser.SellerProfile != null)
            {
                reportedUser.SellerProfile.Status = SellerProfileStatus.Suspended;
                reportedUser.SellerProfile.UpdatedAt = now;
            }
        }
        else if (request.SuspendSellerShop && reportedUser.SellerProfile != null)
        {
            reportedUser.SellerProfile.Status = SellerProfileStatus.Suspended;
            reportedUser.SellerProfile.UpdatedAt = now;
        }

        Guid? createdPenaltyId = null;
        string? appliedPenaltyName = null;

        if (request.PenaltyApplied.HasValue || request.ScoreDeduction > 0 || request.BanAccount || request.SuspendSellerShop)
        {
            var effectivePenaltyType = request.PenaltyApplied
                ?? (request.BanAccount
                    ? PenaltyType.BanAccount
                    : request.SuspendSellerShop
                        ? PenaltyType.Restriction
                        : PenaltyType.DeductPoints);

            var mappedViolation = report.ViolationType switch
            {
                ReportViolationType.Fraud => PenaltyViolationType.Fraud,
                ReportViolationType.Counterfeit => PenaltyViolationType.Fraud,
                _ => PenaltyViolationType.Other
            };

            var penalty = new UserPenalty
            {
                Id = Guid.NewGuid(),
                UserId = reportedUser.Id,
                TargetRole = effectiveTargetRole,
                ViolationType = mappedViolation,
                Reason = request.ResolutionNote.Trim(),
                PenaltyApplied = effectivePenaltyType,
                PointsDeducted = actualDeducted,
                Status = PenaltyStatus.Active,
                PolicyClauseCode = request.PolicyClauseCode,
                IssuedAt = now,
                ExpiresAt = request.DurationDays.HasValue ? now.AddDays(request.DurationDays.Value) : null,
                CreatedBy = actorId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.UserPenalties.Add(penalty);
            createdPenaltyId = penalty.Id;
            appliedPenaltyName = effectivePenaltyType.ToEnumString();
        }

        _dbContext.AddAuditLog(
            action: AuditAction.ReportResolve,
            entityName: nameof(UserReport),
            entityId: report.Id.ToString(),
            oldValues: new { Status = ReportStatus.Pending.ToEnumString() },
            newValues: new
            {
                Status = ReportStatus.Resolved.ToEnumString(),
                TargetRole = effectiveTargetRole.ToEnumString(),
                PenaltyApplied = appliedPenaltyName,
                ScoreDeducted = actualDeducted,
                ResolutionNote = report.ResolutionNote
            },
            userId: actorId.ToString(),
            changedColumns: new[] { nameof(UserReport.Status), nameof(UserReport.ResolvedBy), nameof(UserReport.ResolvedAt), nameof(UserReport.ResolutionNote) });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ResolveReportResponse
        {
            ReportId = report.Id,
            ReportedUserId = report.ReportedUserId,
            Status = report.Status.ToEnumString(),
            TargetRole = effectiveTargetRole.ToEnumString(),
            PenaltyApplied = appliedPenaltyName,
            ScoreDeducted = actualDeducted,
            ReportedUserNewScore = updatedScore,
            PenaltyId = createdPenaltyId,
            ResolvedBy = actorId,
            ResolvedAt = now,
            ResolutionNote = report.ResolutionNote
        };
    }

    public async Task<ResolveReportResponse> DismissReportAsync(
        Guid reportId,
        DismissReportRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var note = request.EffectiveNote;
        if (string.IsNullOrWhiteSpace(note) || note.Trim().Length < 10)
        {
            throw new DomainException("VAL_INVALID_INPUT", "Resolution note must be at least 10 characters long.", 400);
        }

        var report = await _dbContext.UserReports
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);

        if (report == null)
        {
            throw new DomainException("REPORT_NOT_FOUND", "User report was not found for the specified identifier.", 404);
        }

        if (report.Status == ReportStatus.Resolved || report.Status == ReportStatus.Rejected)
        {
            throw new DomainException("REPORT_ALREADY_PROCESSED", "This report has already been resolved or dismissed.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        report.Status = ReportStatus.Rejected;
        report.ResolvedBy = actorId;
        report.ResolvedAt = now;
        report.ResolutionNote = note.Trim();
        report.UpdatedAt = now;

        _dbContext.AddAuditLog(
            action: AuditAction.ReportDismiss,
            entityName: nameof(UserReport),
            entityId: report.Id.ToString(),
            oldValues: new { Status = ReportStatus.Pending.ToEnumString() },
            newValues: new { Status = ReportStatus.Rejected.ToEnumString(), ResolutionNote = report.ResolutionNote },
            userId: actorId.ToString(),
            changedColumns: new[] { nameof(UserReport.Status), nameof(UserReport.ResolvedBy), nameof(UserReport.ResolvedAt), nameof(UserReport.ResolutionNote) });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ResolveReportResponse
        {
            ReportId = report.Id,
            ReportedUserId = report.ReportedUserId,
            Status = report.Status.ToEnumString(),
            TargetRole = report.TargetRole.ToEnumString(),
            PenaltyApplied = null,
            ScoreDeducted = 0,
            ReportedUserNewScore = null,
            PenaltyId = null,
            ResolvedBy = actorId,
            ResolvedAt = now,
            ResolutionNote = report.ResolutionNote
        };
    }

    private static async Task<PagedData<ReportListItemDto>> ExecutePagedReportQueryAsync(
        IQueryable<UserReport> query,
        int safePage,
        int safeLimit,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);

        var rawItems = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((safePage - 1) * safeLimit)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);

        var items = rawItems.Select(r => new ReportListItemDto
        {
            ReportId = r.Id,
            ReporterId = r.ReporterId,
            ReportedUserId = r.ReportedUserId,
            TargetRole = r.TargetRole.ToEnumString(),
            ReferenceType = r.ReferenceType.HasValue ? r.ReferenceType.Value.ToEnumString() : null,
            ReferenceId = r.ReferenceId,
            ViolationType = r.ViolationType.ToEnumString(),
            Description = r.Description,
            EvidenceUrls = r.EvidenceUrls ?? new List<string>(),
            Status = r.Status.ToEnumString(),
            ResolvedBy = r.ResolvedBy,
            ResolvedAt = r.ResolvedAt,
            ResolutionNote = r.ResolutionNote,
            CreatedAt = r.CreatedAt
        }).ToList();

        return PagedData<ReportListItemDto>.From(items, safePage, safeLimit, totalCount);
    }

    public async Task<UploadMediaFilesResponse> UploadReportFilesAsync(
        Guid userId,
        IReadOnlyList<UploadMediaFileInput> files,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "User identifier is required.");
        }

        if (files == null || files.Count == 0)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "At least one evidence file is required.");
        }

        if (files.Count > 5)
        {
            throw new BusinessRuleValidationException("FILE_LIMIT_EXCEEDED", "Maximum limit of 5 attached evidence files exceeded.");
        }

        if (_fileStorageService == null)
        {
            throw new DomainException("STORAGE_UPLOAD_FAILED", "File storage service is not configured.", 500);
        }

        var validatedFlags = new List<bool>(files.Count);
        foreach (var file in files)
        {
            var isVideo = FileValidationHelper.ValidateReportMedia(
                file.Content,
                file.FileName,
                file.SizeBytes,
                maxImageMb: 5,
                maxVideoMb: 20);
            validatedFlags.Add(isVideo);
        }

        var urls = new List<string>(files.Count);
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var stored = await _fileStorageService.UploadAsync(
                file.Content,
                file.FileName,
                file.ContentType,
                folder: $"reports/{userId}",
                isPrivate: false,
                isVideo: validatedFlags[i],
                cancellationToken: cancellationToken);
            urls.Add(stored.Url);
        }

        _logger.LogInformation("[REPORT UPLOAD] Uploaded {Count} evidence file(s) for UserId '{UserId}'.", urls.Count, userId);
        return new UploadMediaFilesResponse(urls);
    }
}
