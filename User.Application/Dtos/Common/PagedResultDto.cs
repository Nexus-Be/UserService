namespace User.Application.Dtos.Common;

using System.Collections.Generic;
using System.Text.Json.Serialization;

public sealed record PagedResultDto<T>(
    [property: JsonPropertyName("items")] IReadOnlyList<T> Items,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("limit")] int Limit
);
