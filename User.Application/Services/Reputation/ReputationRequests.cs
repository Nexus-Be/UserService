namespace User.Application.Services.Reputation;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using User.Domain.Enums;

public sealed record AdjustReputationRequest
{
    public TargetRole TargetRole { get; init; } = TargetRole.Buyer;

    [Range(-50, 50, ErrorMessage = "Score delta must be between -50 and 50.")]
    public int ScoreDelta { get; init; }

    [JsonPropertyName("delta")]
    public int? Delta { get; init; }

    [JsonIgnore]
    public int EffectiveDelta => ScoreDelta != 0 ? ScoreDelta : (Delta ?? 0);

    [Required(ErrorMessage = "Adjustment reason is required.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Adjustment reason must be between 10 and 500 characters long.")]
    public string Reason { get; init; } = string.Empty;

    public ReferenceType? ReferenceType { get; init; }
    public string? ReferenceId { get; init; }
}
