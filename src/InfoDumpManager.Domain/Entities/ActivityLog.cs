using System;
using System.Text.Json;
using InfoDumpManager.Domain.Common;

namespace InfoDumpManager.Domain.Entities;

public sealed class ActivityLog : AuditEntity<Guid>, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public ActivityEventType EventType { get; private set; }
    public string EntityName { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public Guid? UserId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public JsonDocument? Metadata { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public byte[]? RowVersion { get; private set; }

    private ActivityLog()
    {
    }

    public static ActivityLog Create(
        Guid tenantId,
        ActivityEventType eventType,
        string entityName,
        string description,
        Guid? entityId = null,
        Guid? userId = null,
        JsonDocument? metadata = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identifier must be provided.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new ArgumentException("Entity name is required.", nameof(entityName));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required.", nameof(description));
        }

        return new ActivityLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventType = eventType,
            EntityName = entityName.Trim(),
            Description = description.Trim(),
            EntityId = entityId,
            UserId = userId,
            Metadata = metadata,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description cannot be empty.", nameof(description));
        }

        Description = description.Trim();
    }

    public void UpdateMetadata(JsonDocument? metadata)
    {
        Metadata = metadata;
    }
}
