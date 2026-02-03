using System.Text.Json;
using InfoDumpManager.Application.Services.Embeddings;
using StackExchange.Redis;

namespace InfoDumpManager.Infrastructure.Services.Embeddings;

public sealed class RedisEmbeddingCache : IEmbeddingCache
{
    private readonly IDatabase _database;

    public RedisEmbeddingCache(IConnectionMultiplexer connection)
    {
        _database = connection.GetDatabase();
    }

    public async Task<float[]?> TryGetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            throw new ArgumentException("Cache key cannot be empty.", nameof(cacheKey));
        }

        var value = await _database.StringGetAsync(cacheKey).ConfigureAwait(false);
        if (!value.HasValue)
        {
            return null;
        }

        return JsonSerializer.Deserialize<float[]>(value!);
    }

    public Task SetAsync(string cacheKey, float[] vector, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            throw new ArgumentException("Cache key cannot be empty.", nameof(cacheKey));
        }

        if (vector.Length == 0)
        {
            throw new ArgumentException("Vector cannot be empty.", nameof(vector));
        }

        var json = JsonSerializer.Serialize(vector);
        return _database.StringSetAsync(cacheKey, json, timeToLive);
    }
}
