using System;
using System.Collections.Generic;
using InfoDumpManager.Domain.Common;

namespace InfoDumpManager.Domain.Entities;

public sealed class Category : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxNameLength = 128;
    private const int MaxDescriptionLength = 512;
    private readonly List<GEM> _gems = new();

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid CreatedById { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public IReadOnlyCollection<GEM> Gems => _gems.AsReadOnly();

    private Category()
    {
    }

    public static Category Create(Guid tenantId, string name, Guid createdById, string? description = null)
    {
        ValidateTenant(tenantId);
        ValidateGuid(createdById, nameof(createdById));
        var normalizedName = NormalizeName(name);
        var normalizedDescription = NormalizeDescription(description);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = normalizedName,
            CreatedById = createdById,
            CreatedAt = DateTimeOffset.UtcNow,
            Description = normalizedDescription
        };

        return category;
    }

    public void UpdateName(string name)
    {
        Name = NormalizeName(name);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateDescription(string? description)
    {
        Description = NormalizeDescription(description);
        UpdatedAt = DateTimeOffset.UtcNow;
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
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static void ValidateTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identifier must be provided.", nameof(tenantId));
        }
    }

    private static void ValidateGuid(Guid value, string paramName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier must be provided.", paramName);
        }
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Category name is required.", nameof(name));
        }

        var trimmed = name.Trim();

        if (trimmed.Length > MaxNameLength)
        {
            throw new ArgumentException($"Category name cannot exceed {MaxNameLength} characters.", nameof(name));
        }

        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var trimmed = description.Trim();

        if (trimmed.Length > MaxDescriptionLength)
        {
            throw new ArgumentException($"Description cannot exceed {MaxDescriptionLength} characters.", nameof(description));
        }

        return trimmed;
    }
}
