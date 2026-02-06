using InfoDumpManager.Domain.Events;
using MediatR;

namespace InfoDumpManager.Application.Common.Events;

public sealed record DomainEventNotification(IDomainEvent Event) : INotification;
