using InfoDumpManager.Application.Common.Events;
using InfoDumpManager.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InfoDumpManager.Infrastructure.Data;

/// <summary>
/// Intercepts SaveChanges to dispatch domain events raised by aggregates.
/// </summary>
public sealed class DomainEventDispatchInterceptor : SaveChangesInterceptor
{
    private readonly IMediator _mediator;

    public DomainEventDispatchInterceptor(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await DispatchDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        var aggregatesWithEvents = context.ChangeTracker
            .Entries()
            .Where(e => e.Entity is IAggregateWithEvents awe && awe.DomainEvents.Count > 0)
            .Select(e => (IAggregateWithEvents)e.Entity)
            .ToList();

        var domainEvents = aggregatesWithEvents
            .SelectMany(a => a.DomainEvents)
            .ToList();

        foreach (var aggregate in aggregatesWithEvents)
        {
            aggregate.ClearDomainEvents();
        }

        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(new DomainEventNotification(domainEvent), cancellationToken);
        }
    }
}
