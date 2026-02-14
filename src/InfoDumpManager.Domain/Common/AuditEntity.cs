namespace InfoDumpManager.Domain.Common;

/// <summary>
/// Base class for write-only audit/log entities that do not participate
/// in domain event dispatch. Use instead of AggregateRoot for simple records.
/// </summary>
public abstract class AuditEntity<TId>
{
    public TId Id { get; protected set; } = default!;
}
