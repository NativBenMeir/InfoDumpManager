namespace InfoDumpManager.WebAPI.Contracts.GEMs;

public sealed class GemQueryParameters
{
    private const int MaxPageSize = 50;
    private const int DefaultPageSize = 20;

    private int _pageSize = DefaultPageSize;

    public int PageNumber { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = (value is > 0 and <= MaxPageSize) ? value : DefaultPageSize;
    }
}
