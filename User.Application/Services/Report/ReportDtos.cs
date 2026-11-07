namespace User.Application.Services.Report;

using System;
using System.Collections.Generic;

public sealed record ReportListItemDto
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
    public Guid? ResolvedBy { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public string? ResolutionNote { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
