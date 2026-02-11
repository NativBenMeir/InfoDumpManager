using System;
using InfoDumpManager.Domain.Common;
using InfoDumpManager.Domain.Events;
using Xunit;

namespace InfoDumpManager.Tests.Unit.Common;

public sealed class AggregateRootDomainEventsTests
{
    [Fact]
    public void RaiseDomainEvent_AddsEventToCollection()
    {
        var aggregate = TestAggregate.Create();

        aggregate.DoSomethingThatRaisesEvent();

        Assert.Single(aggregate.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var aggregate = TestAggregate.Create();

        aggregate.DoSomethingThatRaisesEvent();
        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    private sealed class TestAggregate : AggregateRoot<Guid>
    {
        private TestAggregate()
        {
        }

        public static TestAggregate Create() => new() { Id = Guid.NewGuid() };

        public void DoSomethingThatRaisesEvent()
        {
            RaiseDomainEvent(new TestDomainEvent(Id, DateTimeOffset.UtcNow));
        }
    }

    private sealed record TestDomainEvent(Guid AggregateId, DateTimeOffset OccurredAt) : IDomainEvent;
}
