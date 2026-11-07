namespace User.Application.Services.Report;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using User.Domain.Enums;

public sealed record CreateReportRequest
{
    [Required(ErrorMessage = "Reported user ID is required.")]
    public Guid ReportedUserId { get; init; }

    public TargetRole TargetRole { get; init; } = TargetRole.Account;

    public ReferenceType? ReferenceType { get; init; }
    public string? ReferenceId { get; init; }

    [Required(ErrorMessage = "Violation type is required.")]
    public ReportViolationType ViolationType { get; init; }

    [Required(ErrorMessage = "Report description is required.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Report description must be between 10 and 2000 characters long.")]
    public string Description { get; init; } = string.Empty;

    public List<string>? EvidenceUrls { get; init; }
}

public sealed record ResolveReportRequest
{
    public TargetRole TargetRole { get; init; } = TargetRole.Account;

    public PenaltyType? PenaltyApplied { get; init; }

    [Range(0, 50, ErrorMessage = "Score deduction must be between 0 and 50 points.")]
    public int ScoreDeduction { get; init; }

    public bool BanAccount { get; init; }
    public bool SuspendSellerShop { get; init; }

    [Range(1, 3650, ErrorMessage = "Duration days must be between 1 and 3650.")]
    public int? DurationDays { get; init; }

    [StringLength(100, ErrorMessage = "Policy clause code must not exceed 100 characters long.")]
    public string? PolicyClauseCode { get; init; }

    [Required(ErrorMessage = "Resolution note is required.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Resolution note must be between 10 and 2000 characters long.")]
    public string ResolutionNote { get; init; } = string.Empty;
}

public sealed record DismissReportRequest
{
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Resolution note must be between 10 and 2000 characters long.")]
    public string ResolutionNote { get; init; } = string.Empty;

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    [JsonIgnore]
    public string EffectiveNote => !string.IsNullOrWhiteSpace(ResolutionNote)
        ? ResolutionNote
        : (Reason ?? string.Empty);
}
