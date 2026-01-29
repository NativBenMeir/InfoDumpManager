namespace InfoDumpManager.Domain.Common;

public abstract class AggregateRoot<TId>
{
    public TId Id { get; protected set; } = default!;
}
