using System;
using InfoDumpManager.Domain.Common;
using InfoDumpManager.Domain.ValueObjects;

namespace InfoDumpManager.Domain.Entities;

public sealed class GEM : AggregateRoot<Guid>, ITenantEntity
{
    public const int MaxTitleLength = 256;
    public const int MaxUrlLength = 2048;

    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public GEMSource Source { get; private set; } = default!;
    public GEMSnapshot Snapshot { get; private set; } = default!;
    public GEMSummary Summary { get; private set; } = GEMSummary.Empty;
    public Guid? CategoryId { get; private set; }
    public Category? Category { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public float[]? TitleEmbedding { get; private set; }
    public float[]? SummaryEmbedding { get; private set; }

    private GEM()
    {
    }

    public static GEM Create(
        Guid tenantId,
        string title,
        string url,
        GEMSource source,
        GEMSnapshot snapshot,
        GEMSummary? summary = null)
    {
        ValidateTenant(tenantId);
        var normalizedTitle = NormalizeTitle(title);
        var normalizedUrl = NormalizeUrl(url);
        var validatedSource = EnsureValueObject(source, nameof(source));
        var validatedSnapshot = EnsureValueObject(snapshot, nameof(snapshot));
        var resolvedSummary = summary ?? GEMSummary.Empty;

        // Create defensive copies of value objects to ensure each GEM has its own instance.
        // This prevents EF Core tracking issues when the same value object reference is shared.
        return new GEM
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = normalizedTitle,
            Url = normalizedUrl,
            Source = validatedSource.Copy(),
            Snapshot = validatedSnapshot.Copy(),
            Summary = resolvedSummary,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void AssignCategory(Category category)
    {
        if (category is null)
        {
            throw new ArgumentNullException(nameof(category));
        }

        if (category.TenantId != TenantId)
        {
            throw new InvalidOperationException("Cannot assign a category from another tenant.");
        }

        Category = category;
        CategoryId = category.Id;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateSummary(GEMSummary summary)
    {
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAsDeleted()
    {
        IsDeleted = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateTitle(string title)
    {
        Title = NormalizeTitle(title);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateTitleEmbedding(float[] embedding)
    {
        if (embedding is null) throw new ArgumentNullException(nameof(embedding));
        TitleEmbedding = embedding;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateSummaryEmbedding(float[] embedding)
    {
        if (embedding is null) throw new ArgumentNullException(nameof(embedding));
        SummaryEmbedding = embedding;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ValidateTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identifier must be provided.", nameof(tenantId));
        }
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        var trimmed = title.Trim();

        if (trimmed.Length > MaxTitleLength)
        {
            throw new ArgumentException($"Title cannot exceed {MaxTitleLength} characters.", nameof(title));
        }

        return trimmed;
    }

    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("GEM URL is required.", nameof(url));
        }

        var trimmed = url.Trim();

        if (trimmed.Length > MaxUrlLength)
        {
            throw new ArgumentException($"GEM URL cannot exceed {MaxUrlLength} characters.", nameof(url));
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("GEM URL must be a well-formed URI.", nameof(url));
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("GEM URL must use http or https scheme.", nameof(url));
        }

        return trimmed;
    }

    private static T EnsureValueObject<T>(T? value, string paramName)
        where T : class
    {
        return value ?? throw new ArgumentNullException(paramName);
    }
}
