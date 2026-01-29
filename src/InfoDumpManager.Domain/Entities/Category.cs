using System;
using System.Collections.Generic;
using InfoDumpManager.Domain.Common;

namespace InfoDumpManager.Domain.Entities;

public sealed class Category : AggregateRoot<Guid>, ITenantEntity
{
    private readonly List<GEM> _gems = new();

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid CreatedById { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<GEM> Gems => _gems.AsReadOnly();

    private Category()
    {
    }

    public static Category Create(Guid tenantId, string name, Guid createdById, string? description = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identifier must be provided.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Category name is required.", nameof(name));
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            CreatedById = createdById,
            CreatedAt = DateTimeOffset.UtcNow,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
        };

        return category;
    }

    public void AddGem(GEM gem)
    {
        if (gem is null)
        {
            throw new ArgumentNullException(nameof(gem));
        }

        if (gem.TenantId != TenantId)
        {
            throw new InvalidOperationException("Cannot assign a GEM from another tenant.");
        }

        gem.AssignCategory(this);

        if (!_gems.Contains(gem))
        {
            _gems.Add(gem);
        }
    }
}
