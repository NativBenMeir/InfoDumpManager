using InfoDumpManager.Application.Services.Caching;
using StackExchange.Redis;

namespace InfoDumpManager.Infrastructure.Services.Caching;

public sealed class RedisTextCache : ITextCache
{
    private readonly IDatabase _database;

    public RedisTextCache(IConnectionMultiplexer connection)
    {
        _database = connection.GetDatabase();
    }

    public async Task<string?> TryGetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            throw new ArgumentException("Cache key cannot be empty.", nameof(cacheKey));
        }

        var value = await _database.StringGetAsync(cacheKey).ConfigureAwait(false);
        return value.HasValue ? value.ToString() : null;
    }

    public Task SetAsync(string cacheKey, string value, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            throw new ArgumentException("Cache key cannot be empty.", nameof(cacheKey));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", nameof(value));
        }

        return _database.StringSetAsync(cacheKey, value, timeToLive);
    }
}
