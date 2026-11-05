namespace User.Application.Services.Internal;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public sealed record CheckReputationRequest(
    [property: JsonPropertyName("action"), Required] string Action,
    [property: JsonPropertyName("userId"), Required] Guid UserId
);

public sealed record VerifyBidEligibilityRequest(
    [property: JsonPropertyName("userId"), Required] Guid UserId
);

public sealed record BatchPublicInfoRequest(
    [property: JsonPropertyName("userIds"), Required, MinLength(1, ErrorMessage = "At least one userId is required."), MaxLength(100, ErrorMessage = "Maximum 100 user IDs are allowed per batch request.")]
    List<Guid> UserIds
);
