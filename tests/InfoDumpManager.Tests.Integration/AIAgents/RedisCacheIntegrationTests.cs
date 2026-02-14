using System.Diagnostics.CodeAnalysis;
using InfoDumpManager.Application.Services.Embeddings;
using InfoDumpManager.Infrastructure.Services.Embeddings;
using InfoDumpManager.Tests.Integration.Fixtures;
using StackExchange.Redis;
using Xunit;

namespace InfoDumpManager.Tests.Integration.AIAgents;

[ExcludeFromCodeCoverage]
[Collection("RedisIntegrationTests")]
public sealed class RedisEmbeddingCacheIntegrationTests : IAsyncLifetime
{
    private readonly RedisTestcontainerFixture _fixture;
    private readonly RedisEmbeddingCache _cache;
    private readonly IDatabase _database;

    public RedisEmbeddingCacheIntegrationTests(RedisTestcontainerFixture fixture)
    {
        _fixture = fixture;
        _cache = new RedisEmbeddingCache(_fixture.ConnectionMultiplexer);
        _database = _fixture.ConnectionMultiplexer.GetDatabase();
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SetAsync_ShouldStoreInRedis()
    {
        var cacheKey = $"test:embedding:key:{Guid.NewGuid():N}";
        var vector = new float[] { 0.1f, 0.2f, 0.3f };
        var ttl = TimeSpan.FromMinutes(10);

        // Act
        await _cache.SetAsync(cacheKey, vector, ttl);

        // Assert against real Redis
        var raw = await _database.StringGetAsync(cacheKey);
        Assert.True(raw.HasValue);

        var fromCache = await _cache.TryGetAsync(cacheKey);
        Assert.NotNull(fromCache);
        Assert.Equal(vector, fromCache!);

        var timeToLive = await _database.KeyTimeToLiveAsync(cacheKey);
        Assert.NotNull(timeToLive);
        Assert.True(timeToLive > TimeSpan.Zero);
        Assert.True(timeToLive <= ttl);
    }

    [Fact]
    public async Task GetAsync_ShouldRetrieveFromCache()
    {
        // Arrange
        var cacheKey = $"test:embedding:get:{Guid.NewGuid():N}";
        var expectedVector = new float[] { 0.11f, 0.22f, 0.33f };
        await _cache.SetAsync(cacheKey, expectedVector, TimeSpan.FromMinutes(1));

        // Act
        var actual = await _cache.TryGetAsync(cacheKey);

        // Assert
        Assert.NotNull(actual);
        Assert.Equal(expectedVector, actual!);
    }

    [Fact]
    public async Task GetAsync_WithExpiredKey_ShouldReturnNull()
    {
        // Arrange
        var cacheKey = $"test:embedding:expired:{Guid.NewGuid():N}";
        await _cache.SetAsync(cacheKey, new float[] { 1f, 2f, 3f }, TimeSpan.FromMilliseconds(150));
        await Task.Delay(300);

        // Act
        var actual = await _cache.TryGetAsync(cacheKey);

        // Assert
        Assert.Null(actual);
    }

    [Fact]
    public async Task Cache_ShouldReduceEmbeddingProviderCalls()
    {
        // Arrange
        var cacheKey = $"test:embedding:cache-hit:{Guid.NewGuid():N}";
        var cachedVector = new float[] { 0.4f, 0.5f, 0.6f };
        await _cache.SetAsync(cacheKey, cachedVector, TimeSpan.FromMinutes(2));

        var provider = new CountingEmbeddingProvider();

        static async Task<float[]> GetOrCreateEmbeddingAsync(
            IEmbeddingCache embeddingCache,
            IEmbeddingProvider provider,
            string key,
            string content)
        {
            var fromCache = await embeddingCache.TryGetAsync(key);
            if (fromCache is not null)
            {
                return fromCache;
            }

            var generated = await provider.GenerateEmbeddingAsync(content, "test-model");
            await embeddingCache.SetAsync(key, generated.Vector, TimeSpan.FromMinutes(5));
            return generated.Vector;
        }

        // Act
        var result = await GetOrCreateEmbeddingAsync(_cache, provider, cacheKey, "content");

        // Assert
        Assert.Equal(cachedVector, result);
        Assert.Equal(0, provider.CallCount);
    }

    private sealed class CountingEmbeddingProvider : IEmbeddingProvider
    {
        public int CallCount { get; private set; }

        public Task<EmbeddingResponse> GenerateEmbeddingAsync(
            string content,
            string model,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new EmbeddingResponse(
                new float[] { 9f, 9f, 9f },
                model,
                "counting-provider",
                10,
                0.001m));
        }
    }
}
