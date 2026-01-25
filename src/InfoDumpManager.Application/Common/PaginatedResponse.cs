using System.Collections.Generic;

namespace InfoDumpManager.Application.Common;

public sealed class PaginatedResponse<T>
{
    public IList<T> Items { get; init; } = new List<T>();
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
