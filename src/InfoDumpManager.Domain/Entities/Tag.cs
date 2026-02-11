using System;
using InfoDumpManager.Domain.Common;

namespace InfoDumpManager.Domain.Entities;

public sealed class Tag : AggregateRoot<Guid>, ITenantEntity
{
    public const int MaxNameLength = 64;

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid CreatedById { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private Tag()
    {
    }

    public static Tag Create(Guid tenantId, string name, Guid createdById)
    {
        ValidateTenant(tenantId);
        ValidateGuid(createdById, nameof(createdById));

        var normalizedName = NormalizeName(name);

        return new Tag
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = normalizedName,
            CreatedById = createdById,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdateName(string name)
    {
        Name = NormalizeName(name);
        UpdatedAt = DateTimeOffset.UtcNow;
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
            throw new ArgumentException("Tag name is required.", nameof(name));
        }

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            throw new ArgumentException($"Tag name cannot exceed {MaxNameLength} characters.", nameof(name));
        }

        return trimmed;
    }
}
