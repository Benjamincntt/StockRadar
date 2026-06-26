namespace StockRadar.Application.Common;

public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
}

public class PaginationQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    public int Skip => Math.Max(0, (Page - 1) * PageSize);

    public void Normalize(int maxPageSize = 100)
    {
        if (Page < 1) Page = 1;
        if (PageSize < 1) PageSize = 1;
        if (PageSize > maxPageSize) PageSize = maxPageSize;
    }
}

public static class PagingExtensions
{
    public static PagedResult<T> ToPagedResult<T>(this IEnumerable<T> source, PaginationQuery query)
    {
        query.Normalize();
        var list = source.ToList();
        var items = list.Skip(query.Skip).Take(query.PageSize).ToList();
        return new PagedResult<T>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = list.Count
        };
    }
}
