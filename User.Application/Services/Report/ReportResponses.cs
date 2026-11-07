namespace User.Application.Services.Report;

using System;
using System.Collections.Generic;

public sealed record ReportDetailResponse
{
    public Guid ReportId { get; init; }
    public Guid ReporterId { get; init; }
    public Guid ReportedUserId { get; init; }
    public string TargetRole { get; init; } = string.Empty;
    public string? ReferenceType { get; init; }
    public string? ReferenceId { get; init; }
    public string ViolationType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> EvidenceUrls { get; init; } = Array.Empty<string>();
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record ResolveReportResponse
{
    public Guid ReportId { get; init; }
    public Guid ReportedUserId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string TargetRole { get; init; } = string.Empty;
    public string? PenaltyApplied { get; init; }
    public int ScoreDeducted { get; init; }
    public int? ReportedUserNewScore { get; init; }
    public Guid? PenaltyId { get; init; }
    public Guid ResolvedBy { get; init; }
    public DateTimeOffset ResolvedAt { get; init; }
    public string ResolutionNote { get; init; } = string.Empty;
}
