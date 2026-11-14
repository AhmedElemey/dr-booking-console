using Microsoft.EntityFrameworkCore;

namespace Console.Api.Common;

public sealed record PagedList<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public static class Paging
{
    public static (int Page, int PageSize) Normalize(int? page, int? pageSize) =>
        (Math.Max(page ?? 1, 1), Math.Clamp(pageSize ?? 20, 1, 100));

    public static async Task<PagedList<T>> ToPageAsync<T>(this IQueryable<T> query, int page, int pageSize, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedList<T>(items, total, page, pageSize);
    }
}
