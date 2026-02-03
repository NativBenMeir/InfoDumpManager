using System.Security.Cryptography;
using System.Text;
using InfoDumpManager.Application.Services.Embeddings;
using Microsoft.Extensions.Logging;

namespace InfoDumpManager.Infrastructure.Services.Embeddings;

/// <summary>
/// Deterministic embedding provider for local development and testing.
/// </summary>
public sealed class DeterministicEmbeddingProvider : IEmbeddingProvider
{
    private const int VectorSize = 1536;
    private readonly ILogger<DeterministicEmbeddingProvider> _logger;

    public DeterministicEmbeddingProvider(ILogger<DeterministicEmbeddingProvider> logger)
    {
        _logger = logger;
    }

    public Task<EmbeddingResponse> GenerateEmbeddingAsync(
        string content,
        string model,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be empty.", nameof(content));
        }

        var vector = new float[VectorSize];
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(content));

        for (var index = 0; index < VectorSize; index++)
        {
            var normalized = hash[index % hash.Length] / 255f;
            vector[index] = (normalized * 2f) - 1f;
        }

        var tokensUsed = (int)(content.Length / 4.0);
        _logger.LogInformation(
            "Generated deterministic embedding for model {Model} with {Tokens} tokens",
            model,
            tokensUsed);

        return Task.FromResult(new EmbeddingResponse(
            vector,
            model,
            "Deterministic",
            tokensUsed,
            0m));
    }
}
