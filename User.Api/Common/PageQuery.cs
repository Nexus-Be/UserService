namespace User.Api.Common;

using Microsoft.AspNetCore.Mvc;
using User.Application.Common;

public sealed class PageQuery
{
    [FromQuery(Name = "page")]
    public int Page { get; set; } = PaginationConstants.DefaultPage;

    [FromQuery(Name = "pageSize")]
    public int PageSize { get; set; } = PaginationConstants.DefaultPageSize;

    public (int Page, int PageSize) Normalize() => PaginationConstants.Normalize(Page, PageSize);
}
