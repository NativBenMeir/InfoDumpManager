using InfoDumpManager.Domain.Events;

namespace InfoDumpManager.Domain.Common;

/// <summary>
/// Non-generic interface for aggregates that raise domain events.
/// Used by the infrastructure interceptor to discover pending events.
/// </summary>
public interface IAggregateWithEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
