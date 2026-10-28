namespace User.Application.Common;

public static class PaginationConstants
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MinPageSize = 1;
    public const int MaxPageSize = 100; // Khớp chuẩn MaxPageSize = 100 của ApiGateway

    /// <summary>
    /// Chuẩn hóa số trang (page >= 1) và kích thước trang (1 <= limit <= 100) an toàn, chống DoS / tràn bộ nhớ.
    /// </summary>
    public static (int Page, int Limit) Normalize(int page, int limit)
    {
        var safePage = page < 1 ? DefaultPage : page;
        var safeLimit = limit switch
        {
            < MinPageSize => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => limit
        };

        return (safePage, safeLimit);
    }
}
