namespace InfoDumpManager.Application.Services.Caching;

/// <summary>
/// Cache for text payloads.
/// </summary>
public interface ITextCache
{
    Task<string?> TryGetAsync(string cacheKey, CancellationToken cancellationToken = default);

    Task SetAsync(string cacheKey, string value, TimeSpan timeToLive, CancellationToken cancellationToken = default);
}
