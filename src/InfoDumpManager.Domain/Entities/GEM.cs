using System;
using InfoDumpManager.Domain.Common;
using InfoDumpManager.Domain.ValueObjects;

namespace InfoDumpManager.Domain.Entities;

public sealed class GEM : AggregateRoot<Guid>, ITenantEntity
{
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
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identifier must be provided.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("GEM URL is required.", nameof(url));
        }

        var gem = new GEM
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title.Trim(),
            Url = url.Trim(),
            Source = source,
            Snapshot = snapshot,
            Summary = summary ?? GEMSummary.Empty,
            CreatedAt = DateTimeOffset.UtcNow
        };

        return gem;
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
}
